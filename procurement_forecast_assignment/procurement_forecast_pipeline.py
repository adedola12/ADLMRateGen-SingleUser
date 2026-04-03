from __future__ import annotations

import argparse
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import joblib
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.compose import ColumnTransformer, TransformedTargetRegressor
from sklearn.ensemble import HistGradientBoostingRegressor, RandomForestRegressor
from sklearn.impute import SimpleImputer
from sklearn.inspection import permutation_importance
from sklearn.metrics import mean_absolute_error, mean_squared_error, r2_score
from sklearn.model_selection import RandomizedSearchCV
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OrdinalEncoder


DEFAULT_GLORY_DIR = Path(r"C:\Users\ADLM\OneDrive\Documentos\KONAMI\Job\glory")


@dataclass
class DatasetBundle:
    raw_data: pd.DataFrame
    prepared_panel: pd.DataFrame
    train_data: pd.DataFrame
    test_data: pd.DataFrame
    feature_columns: list[str]
    quality_summary: pd.DataFrame


def parse_args() -> argparse.Namespace:
    script_dir = Path(__file__).resolve().parent
    default_input = script_dir / "Procurement Dataset.xlsx"
    if not default_input.exists():
        default_input = DEFAULT_GLORY_DIR / "Procurement Dataset.xlsx"

    parser = argparse.ArgumentParser(
        description="Train and evaluate a procurement demand forecasting workflow."
    )
    parser.add_argument("--input", type=Path, default=default_input)
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=script_dir / "procurement_forecast_outputs",
    )
    parser.add_argument("--test-start", type=str, default="2026-02-01")
    parser.add_argument("--enable-search", action="store_true")
    parser.add_argument("--random-search-iter", type=int, default=4)
    return parser.parse_args()


def normalize_text(value: Any) -> str | None:
    if pd.isna(value):
        return None
    text = str(value).strip().lower()
    if not text or text in {"nan", "none"}:
        return None
    text = text.replace("&", " and ")
    text = re.sub(r"[^a-z0-9]+", " ", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text or None


def normalize_uom(value: Any) -> str | None:
    text = normalize_text(value)
    if text is None:
        return None

    mapping = {
        "tons": "ton",
        "ton": "ton",
        "bags": "bag",
        "bag": "bag",
        "pcs": "pcs",
        "pc": "pcs",
        "piece": "pcs",
        "pieces": "pcs",
        "litre": "litre",
        "litres": "litre",
        "ltrs": "litre",
        "ltr": "litre",
        "length": "length",
        "lenght": "length",
        "lengths": "length",
        "lenghts": "length",
        "roll": "roll",
        "rolls": "roll",
        "bundle": "bundle",
        "bundles": "bundle",
        "pkt": "pkt",
        "pkts": "pkt",
        "set": "set",
        "sets": "set",
        "nos": "nos",
        "no": "nos",
        "no s": "nos",
        "no's": "nos",
    }
    return mapping.get(text, text)


def safe_replace_year(timestamp: pd.Timestamp, expected_year: int) -> pd.Timestamp:
    try:
        return timestamp.replace(year=expected_year)
    except ValueError:
        return timestamp


def load_raw_workbook(input_path: Path) -> tuple[pd.DataFrame, int]:
    workbook = pd.ExcelFile(input_path)
    frames: list[pd.DataFrame] = []

    for sheet_name in workbook.sheet_names:
        sheet_df = workbook.parse(sheet_name, header=1)
        sheet_df.columns = [str(col).strip() for col in sheet_df.columns]
        sheet_df["source_sheet"] = sheet_name
        frames.append(sheet_df)

    raw = pd.concat(frames, ignore_index=True, sort=False)
    raw["Date"] = pd.to_datetime(raw["Date"], errors="coerce")
    raw["expected_year"] = raw["source_sheet"].str.extract(r"(20\d{2})").astype(float)[0]

    mismatch_mask = (
        raw["Date"].notna()
        & raw["expected_year"].notna()
        & raw["Date"].dt.year.ne(raw["expected_year"].astype(int))
    )

    corrected_rows = int(mismatch_mask.sum())
    raw.loc[mismatch_mask, "Date"] = [
        safe_replace_year(date_value, int(expected_year))
        for date_value, expected_year in zip(
            raw.loc[mismatch_mask, "Date"],
            raw.loc[mismatch_mask, "expected_year"],
        )
    ]
    return raw, corrected_rows


def build_dataset_bundle(input_path: Path, test_start: str) -> DatasetBundle:
    raw, corrected_rows = load_raw_workbook(input_path)

    raw_rows = int(len(raw))
    missing_date_before = int(raw["Date"].isna().sum())
    missing_qty_before = int(pd.to_numeric(raw["Qty Recievd"], errors="coerce").isna().sum())
    missing_location_before = int(raw["Location"].isna().sum())

    clean = raw.copy()
    for column in ["Location", "Item", "Description"]:
        clean[column] = clean[column].apply(normalize_text)

    clean["UOM"] = clean["UOM"].apply(normalize_uom)
    clean["Qty Recievd"] = pd.to_numeric(clean["Qty Recievd"], errors="coerce")
    clean = clean.dropna(subset=["Date", "Qty Recievd", "Location"]).copy()
    clean["item_group"] = clean["Item"].fillna(clean["Description"])
    clean["month"] = clean["Date"].dt.to_period("M").dt.to_timestamp()

    monthly = (
        clean.groupby(["month", "Location", "item_group", "UOM"], dropna=False)["Qty Recievd"]
        .sum()
        .reset_index(name="monthly_qty")
    )

    all_months = pd.date_range(monthly["month"].min(), monthly["month"].max(), freq="MS")
    all_series = monthly[["Location", "item_group", "UOM"]].drop_duplicates()
    panel = (
        all_series.assign(_merge_key=1)
        .merge(pd.DataFrame({"month": all_months, "_merge_key": 1}), on="_merge_key")
        .drop(columns="_merge_key")
        .merge(monthly, on=["month", "Location", "item_group", "UOM"], how="left")
    )

    panel["monthly_qty"] = panel["monthly_qty"].fillna(0.0)
    panel = panel.sort_values(["Location", "item_group", "UOM", "month"]).reset_index(drop=True)

    group_keys = ["Location", "item_group", "UOM"]
    grouped = panel.groupby(group_keys, dropna=False)
    for lag_number in [1, 2, 3]:
        panel[f"lag_{lag_number}"] = grouped["monthly_qty"].shift(lag_number).fillna(0.0)

    panel["rolling_mean_3"] = grouped["monthly_qty"].transform(
        lambda series: series.shift(1).rolling(3, min_periods=1).mean()
    ).fillna(0.0)
    panel["rolling_std_3"] = grouped["monthly_qty"].transform(
        lambda series: series.shift(1).rolling(3, min_periods=2).std()
    ).fillna(0.0)
    panel["non_zero_lag_count_3"] = (panel[["lag_1", "lag_2", "lag_3"]] > 0).sum(axis=1)
    panel["month_num"] = panel["month"].dt.month
    panel["quarter"] = panel["month"].dt.quarter
    panel["year"] = panel["month"].dt.year
    panel["month_sin"] = np.sin(2 * np.pi * panel["month_num"] / 12)
    panel["month_cos"] = np.cos(2 * np.pi * panel["month_num"] / 12)
    panel["series_id"] = (
        panel["Location"].fillna("unknown")
        + " | "
        + panel["item_group"].fillna("unknown")
        + " | "
        + panel["UOM"].fillna("unknown")
    )

    feature_columns = [
        "Location",
        "item_group",
        "UOM",
        "month_num",
        "quarter",
        "year",
        "month_sin",
        "month_cos",
        "lag_1",
        "lag_2",
        "lag_3",
        "rolling_mean_3",
        "rolling_std_3",
        "non_zero_lag_count_3",
    ]

    test_start_timestamp = pd.Timestamp(test_start)
    train_data = panel.loc[panel["month"] < test_start_timestamp].copy().reset_index(drop=True)
    test_data = panel.loc[panel["month"] >= test_start_timestamp].copy().reset_index(drop=True)

    quality_summary = pd.DataFrame(
        [
            {"metric": "raw_rows", "value": raw_rows},
            {"metric": "rows_after_cleaning", "value": int(len(clean))},
            {"metric": "corrected_date_rows", "value": corrected_rows},
            {"metric": "missing_date_rows_before_cleaning", "value": missing_date_before},
            {"metric": "missing_qty_rows_before_cleaning", "value": missing_qty_before},
            {"metric": "missing_location_rows_before_cleaning", "value": missing_location_before},
            {"metric": "monthly_panel_rows", "value": int(len(panel))},
            {"metric": "unique_series", "value": int(panel[group_keys].drop_duplicates().shape[0])},
            {"metric": "unique_months", "value": int(panel["month"].nunique())},
            {"metric": "train_rows", "value": int(len(train_data))},
            {"metric": "test_rows", "value": int(len(test_data))},
            {"metric": "positive_test_rows", "value": int((test_data["monthly_qty"] > 0).sum())},
            {"metric": "date_min", "value": str(clean["Date"].min().date())},
            {"metric": "date_max", "value": str(clean["Date"].max().date())},
        ]
    )

    return DatasetBundle(
        raw_data=clean,
        prepared_panel=panel,
        train_data=train_data,
        test_data=test_data,
        feature_columns=feature_columns,
        quality_summary=quality_summary,
    )


def create_preprocessor(feature_columns: list[str]) -> ColumnTransformer:
    categorical_columns = ["Location", "item_group", "UOM"]
    numeric_columns = [column for column in feature_columns if column not in categorical_columns]

    return ColumnTransformer(
        transformers=[
            (
                "num",
                Pipeline([("imputer", SimpleImputer(strategy="median"))]),
                numeric_columns,
            ),
            (
                "cat",
                Pipeline(
                    [
                        ("imputer", SimpleImputer(strategy="most_frequent")),
                        (
                            "encoder",
                            OrdinalEncoder(
                                handle_unknown="use_encoded_value",
                                unknown_value=-1,
                                encoded_missing_value=-1,
                            ),
                        ),
                    ]
                ),
                categorical_columns,
            ),
        ]
    )


def make_wrapped_model(base_estimator: Any, feature_columns: list[str]) -> TransformedTargetRegressor:
    return TransformedTargetRegressor(
        regressor=Pipeline(
            [
                ("preprocessor", create_preprocessor(feature_columns)),
                ("model", base_estimator),
            ]
        ),
        func=np.log1p,
        inverse_func=np.expm1,
    )


def build_candidate_models(feature_columns: list[str]) -> dict[str, TransformedTargetRegressor]:
    return {
        "random_forest": make_wrapped_model(
            RandomForestRegressor(
                n_estimators=300,
                max_depth=16,
                min_samples_leaf=2,
                random_state=42,
                n_jobs=-1,
            ),
            feature_columns,
        ),
        "hist_gradient_boosting": make_wrapped_model(
            HistGradientBoostingRegressor(
                max_depth=8,
                learning_rate=0.05,
                max_iter=300,
                min_samples_leaf=10,
                random_state=42,
            ),
            feature_columns,
        ),
    }


def build_search_spaces(
    feature_columns: list[str],
    random_search_iter: int,
    cv_splits: list[tuple[np.ndarray, np.ndarray]],
) -> dict[str, RandomizedSearchCV]:
    base_models = {
        "random_forest": RandomForestRegressor(random_state=42, n_jobs=-1),
        "hist_gradient_boosting": HistGradientBoostingRegressor(random_state=42),
    }
    parameter_spaces = {
        "random_forest": {
            "regressor__model__n_estimators": [150, 250, 350],
            "regressor__model__max_depth": [8, 12, 16, None],
            "regressor__model__min_samples_leaf": [1, 2, 4, 8],
            "regressor__model__max_features": ["sqrt", 0.5, None],
        },
        "hist_gradient_boosting": {
            "regressor__model__max_depth": [4, 6, 8, 10],
            "regressor__model__learning_rate": [0.03, 0.05, 0.08, 0.1],
            "regressor__model__max_iter": [150, 250, 350],
            "regressor__model__min_samples_leaf": [5, 10, 20, 30],
            "regressor__model__l2_regularization": [0.0, 0.1, 1.0],
        },
    }

    searches: dict[str, RandomizedSearchCV] = {}
    for model_name, estimator in base_models.items():
        searches[model_name] = RandomizedSearchCV(
            estimator=make_wrapped_model(estimator, feature_columns),
            param_distributions=parameter_spaces[model_name],
            n_iter=random_search_iter,
            scoring="neg_mean_absolute_error",
            cv=cv_splits,
            random_state=42,
            n_jobs=1,
            refit=True,
        )

    return searches


def build_expanding_month_cv(train_data: pd.DataFrame) -> list[tuple[np.ndarray, np.ndarray]]:
    unique_months = sorted(train_data["month"].unique())
    splits: list[tuple[np.ndarray, np.ndarray]] = []

    for split_index in range(3, len(unique_months)):
        train_indices = train_data.index[
            train_data["month"].isin(unique_months[:split_index])
        ].to_numpy()
        validation_indices = train_data.index[
            train_data["month"] == unique_months[split_index]
        ].to_numpy()
        if len(train_indices) > 0 and len(validation_indices) > 0:
            splits.append((train_indices, validation_indices))

    return splits


def root_mean_squared_error(y_true: np.ndarray, y_pred: np.ndarray) -> float:
    return float(math.sqrt(mean_squared_error(y_true, y_pred)))


def safe_mape(y_true: np.ndarray, y_pred: np.ndarray) -> float:
    denominator = np.maximum(np.abs(y_true), 1.0)
    return float(np.mean(np.abs(y_true - y_pred) / denominator) * 100)


def positive_only_mape(y_true: np.ndarray, y_pred: np.ndarray) -> float:
    positive_mask = y_true > 0
    if not np.any(positive_mask):
        return float("nan")
    return float(
        np.mean(np.abs((y_true[positive_mask] - y_pred[positive_mask]) / y_true[positive_mask]))
        * 100
    )


def weighted_mape(y_true: np.ndarray, y_pred: np.ndarray) -> float:
    denominator = float(np.abs(y_true).sum())
    if denominator == 0:
        return float("nan")
    return float(np.abs(y_true - y_pred).sum() / denominator * 100)


def collect_metrics(y_true: np.ndarray, y_pred: np.ndarray) -> dict[str, float]:
    return {
        "mae": float(mean_absolute_error(y_true, y_pred)),
        "rmse": root_mean_squared_error(y_true, y_pred),
        "safe_mape": safe_mape(y_true, y_pred),
        "positive_only_mape": positive_only_mape(y_true, y_pred),
        "wmape": weighted_mape(y_true, y_pred),
        "r2": float(r2_score(y_true, y_pred)),
    }


def train_and_evaluate(
    bundle: DatasetBundle,
    enable_search: bool,
    random_search_iter: int,
) -> tuple[dict[str, Any], pd.DataFrame, pd.DataFrame, str]:
    train_data = bundle.train_data
    test_data = bundle.test_data
    feature_columns = bundle.feature_columns

    X_train = train_data[feature_columns]
    y_train = train_data["monthly_qty"].to_numpy()
    X_test = test_data[feature_columns]
    y_test = test_data["monthly_qty"].to_numpy()

    baseline_predictions = test_data["lag_1"].clip(lower=0.0).to_numpy()
    predictions_frame = test_data[
        ["month", "series_id", "Location", "item_group", "UOM", "monthly_qty", "lag_1"]
    ].copy()
    predictions_frame = predictions_frame.rename(
        columns={"monthly_qty": "actual_qty", "lag_1": "naive_lag_1_prediction"}
    )

    metrics_rows: list[dict[str, Any]] = []
    fitted_models: dict[str, Any] = {}

    baseline_metrics = collect_metrics(y_test, baseline_predictions)
    metrics_rows.append({"model": "naive_lag_1", **baseline_metrics, "best_params": None})

    if enable_search:
        candidate_estimators: dict[str, Any] = build_search_spaces(
            feature_columns=feature_columns,
            random_search_iter=random_search_iter,
            cv_splits=build_expanding_month_cv(train_data),
        )
    else:
        candidate_estimators = build_candidate_models(feature_columns)

    for model_name, estimator in candidate_estimators.items():
        estimator.fit(X_train, y_train)

        if enable_search:
            fitted_model = estimator.best_estimator_
            best_params = estimator.best_params_
        else:
            fitted_model = estimator
            best_params = None

        predictions = np.clip(fitted_model.predict(X_test), 0, None)
        fitted_models[model_name] = fitted_model
        predictions_frame[f"{model_name}_prediction"] = predictions

        model_metrics = collect_metrics(y_test, predictions)
        metrics_rows.append({"model": model_name, **model_metrics, "best_params": best_params})

    metrics_frame = pd.DataFrame(metrics_rows).sort_values(
        ["wmape", "mae", "rmse"], ascending=[True, True, True]
    )

    non_baseline_metrics = metrics_frame.loc[metrics_frame["model"] != "naive_lag_1"].copy()
    champion_model_name = non_baseline_metrics.iloc[0]["model"]
    return fitted_models, metrics_frame, predictions_frame, champion_model_name


def convert_for_json(value: Any) -> Any:
    if isinstance(value, (np.integer, np.floating)):
        return value.item()
    if isinstance(value, pd.Timestamp):
        return str(value)
    if isinstance(value, dict):
        return {str(key): convert_for_json(val) for key, val in value.items()}
    if isinstance(value, list):
        return [convert_for_json(item) for item in value]
    return value


def save_feature_importance(
    champion_model: Any,
    test_data: pd.DataFrame,
    feature_columns: list[str],
    output_dir: Path,
) -> pd.DataFrame:
    permutation = permutation_importance(
        champion_model,
        test_data[feature_columns],
        test_data["monthly_qty"].to_numpy(),
        scoring="neg_mean_absolute_error",
        random_state=42,
        n_repeats=5,
        n_jobs=1,
    )

    importance_frame = pd.DataFrame(
        {
            "feature": feature_columns,
            "importance_mean": permutation.importances_mean,
            "importance_std": permutation.importances_std,
        }
    ).sort_values("importance_mean", ascending=False)
    importance_frame.to_csv(output_dir / "champion_feature_importance.csv", index=False)

    top_features = importance_frame.head(10).iloc[::-1]
    plt.figure(figsize=(10, 6))
    plt.barh(top_features["feature"], top_features["importance_mean"], color="#1f77b4")
    plt.xlabel("Permutation importance (MAE decrease)")
    plt.ylabel("Feature")
    plt.title("Champion Model Feature Importance")
    plt.tight_layout()
    plt.savefig(output_dir / "champion_feature_importance.png", dpi=200)
    plt.close()
    return importance_frame


def save_plots(
    metrics_frame: pd.DataFrame,
    predictions_frame: pd.DataFrame,
    champion_model_name: str,
    output_dir: Path,
) -> None:
    comparison_metrics = metrics_frame.copy()
    comparison_metrics["label"] = comparison_metrics["model"].str.replace("_", " ").str.title()

    figure, axes = plt.subplots(1, 3, figsize=(15, 5))
    for axis, metric_name, color in zip(
        axes,
        ["mae", "rmse", "wmape"],
        ["#1f77b4", "#ff7f0e", "#2ca02c"],
    ):
        axis.bar(comparison_metrics["label"], comparison_metrics[metric_name], color=color)
        axis.set_title(metric_name.upper())
        axis.tick_params(axis="x", rotation=30)
    figure.suptitle("Model Comparison on Holdout Set")
    figure.tight_layout()
    figure.savefig(output_dir / "model_comparison.png", dpi=200)
    plt.close(figure)

    actual_values = predictions_frame["actual_qty"].to_numpy()
    champion_predictions = predictions_frame[f"{champion_model_name}_prediction"].to_numpy()
    residuals = actual_values - champion_predictions

    plt.figure(figsize=(6, 6))
    plt.scatter(actual_values, champion_predictions, alpha=0.35, color="#1f77b4")
    max_value = max(np.max(actual_values), np.max(champion_predictions), 1.0)
    plt.plot([0, max_value], [0, max_value], linestyle="--", color="black")
    plt.xlabel("Actual Quantity")
    plt.ylabel("Predicted Quantity")
    plt.title(f"{champion_model_name.replace('_', ' ').title()} Actual vs Predicted")
    plt.tight_layout()
    plt.savefig(output_dir / "champion_actual_vs_predicted.png", dpi=200)
    plt.close()

    plt.figure(figsize=(6, 6))
    plt.scatter(champion_predictions, residuals, alpha=0.35, color="#d62728")
    plt.axhline(0, linestyle="--", color="black")
    plt.xlabel("Predicted Quantity")
    plt.ylabel("Residual (Actual - Predicted)")
    plt.title(f"{champion_model_name.replace('_', ' ').title()} Residual Plot")
    plt.tight_layout()
    plt.savefig(output_dir / "champion_residual_plot.png", dpi=200)
    plt.close()


def save_outputs(
    bundle: DatasetBundle,
    fitted_models: dict[str, Any],
    metrics_frame: pd.DataFrame,
    predictions_frame: pd.DataFrame,
    champion_model_name: str,
    output_dir: Path,
    enable_search: bool,
    random_search_iter: int,
) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)

    bundle.quality_summary.to_csv(output_dir / "data_quality_summary.csv", index=False)
    bundle.prepared_panel.to_csv(output_dir / "prepared_monthly_panel.csv", index=False)

    metrics_to_save = metrics_frame.copy()
    metrics_to_save["best_params"] = metrics_to_save["best_params"].apply(
        lambda value: json.dumps(convert_for_json(value)) if isinstance(value, dict) else ""
    )
    metrics_to_save.to_csv(output_dir / "metrics_summary.csv", index=False)
    predictions_frame.to_csv(output_dir / "test_predictions.csv", index=False)

    champion_model = fitted_models[champion_model_name]
    joblib.dump(champion_model, output_dir / f"{champion_model_name}_model.joblib")

    importance_frame = save_feature_importance(
        champion_model=champion_model,
        test_data=bundle.test_data,
        feature_columns=bundle.feature_columns,
        output_dir=output_dir,
    )
    save_plots(metrics_frame, predictions_frame, champion_model_name, output_dir)

    summary_payload = {
        "output_dir": str(output_dir),
        "test_start_month": str(bundle.test_data["month"].min().date()),
        "champion_model": champion_model_name,
        "enable_search": enable_search,
        "random_search_iter": random_search_iter,
        "top_feature": importance_frame.iloc[0]["feature"] if not importance_frame.empty else None,
        "top_wmape_model": metrics_frame.iloc[0]["model"],
    }
    with (output_dir / "run_summary.json").open("w", encoding="utf-8") as file_handle:
        json.dump(convert_for_json(summary_payload), file_handle, indent=2)


def print_console_summary(
    metrics_frame: pd.DataFrame,
    champion_model_name: str,
    output_dir: Path,
) -> None:
    print("\nSaved outputs")
    print(output_dir)
    print("\nModel metrics")
    print(
        metrics_frame[
            ["model", "mae", "rmse", "safe_mape", "positive_only_mape", "wmape", "r2"]
        ].to_string(index=False)
    )
    print(f"\nChampion model: {champion_model_name}")
    print("Note: classical MAPE is unstable here because many holdout rows have zero demand.")


def main() -> None:
    args = parse_args()
    bundle = build_dataset_bundle(args.input, args.test_start)
    bundle.raw_data.attrs["input_path"] = str(args.input)

    fitted_models, metrics_frame, predictions_frame, champion_model_name = train_and_evaluate(
        bundle=bundle,
        enable_search=args.enable_search,
        random_search_iter=args.random_search_iter,
    )

    save_outputs(
        bundle=bundle,
        fitted_models=fitted_models,
        metrics_frame=metrics_frame,
        predictions_frame=predictions_frame,
        champion_model_name=champion_model_name,
        output_dir=args.output_dir,
        enable_search=args.enable_search,
        random_search_iter=args.random_search_iter,
    )
    print_console_summary(metrics_frame, champion_model_name, args.output_dir)


if __name__ == "__main__":
    main()
