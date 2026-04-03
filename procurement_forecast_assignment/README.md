# Procurement Forecast Assignment

This folder contains a complete Python workflow for the Module 4 procurement forecasting assignment. It reads the Excel workbook, cleans inconsistent sheet dates and text labels, builds monthly lag features, trains two regression models, compares them with a naive lag baseline, and saves outputs for your report.

## Files

- `procurement_forecast_pipeline.py`: main training and evaluation script
- `run_pipeline.ps1`: simple Windows runner
- `requirements.txt`: Python packages required

## What The Script Saves

The output folder is `procurement_forecast_outputs` and includes:

- `metrics_summary.csv`
- `test_predictions.csv`
- `prepared_monthly_panel.csv`
- `data_quality_summary.csv`
- `model_comparison.png`
- `champion_actual_vs_predicted.png`
- `champion_residual_plot.png`
- `champion_feature_importance.csv`
- `champion_feature_importance.png`
- `run_summary.json`
- `<champion_model>_model.joblib`

## Install Packages

```powershell
python -m pip install -r requirements.txt
```

Or install directly:

```powershell
python -m pip install pandas scikit-learn matplotlib openpyxl joblib
```

## Run The Workflow

From this folder:

```powershell
.\run_pipeline.ps1
```

Or:

```powershell
python .\procurement_forecast_pipeline.py
```

To run the optional randomized search before testing:

```powershell
.\run_pipeline.ps1 -EnableSearch
```

## Modeling Notes

- Unit of analysis: monthly quantity by `Location + Item Group + UOM`
- Baseline: naive lag-1 forecast
- Candidate models: `RandomForestRegressor` and `HistGradientBoostingRegressor`
- Leakage control: chronological split with the holdout starting at `2026-02-01`
- Feature engineering: lag 1/2/3, rolling mean, rolling standard deviation, month, quarter, and cyclical month terms

## How To Check That It Worked

1. Confirm the console prints the output folder path and a metrics table.
2. Open `metrics_summary.csv` and verify all three models appear.
3. Open `model_comparison.png`, `champion_actual_vs_predicted.png`, and `champion_residual_plot.png`.
4. Open `test_predictions.csv` to inspect actual vs predicted monthly quantities.
5. Open `data_quality_summary.csv` to document cleaning decisions in your report.

## Important Interpretation Note

This dataset contains many zero-demand months, so classical MAPE becomes unstable and can look much worse than MAE or RMSE suggest. The script therefore also saves `positive_only_mape` and `wmape`, which are more defensible for this assignment when discussing zero-inflated demand.
