# Module 4 Report Guide

Use this structure for the 1,800 to 2,200 word report and insert the values from `procurement_forecast_outputs`.

## 1. Business Need And Target Definition

State that the business objective is to forecast next-month procurement demand early enough to support bulk buying and reduce reactive purchasing. Define the target as monthly quantity by `Location + Item Group + UOM`. Explain that the forecast horizon is one month ahead and that success is judged with MAE, RMSE, R2, and error-rate measures that remain interpretable under zero-demand months.

## 2. Data Readiness And Feature Set

Document that the workbook contains weekly procurement tabs from September 2025 to March 2026. Explain the cleaning steps:

- corrected date-year mismatches using the sheet year
- standardized text labels for location, item, and UOM
- converted `Qty Recievd` to numeric
- aggregated daily rows into monthly totals
- expanded the panel so missing monthly demand becomes explicit zero demand

List the engineered features:

- lag 1, lag 2, lag 3 monthly demand
- 3-month rolling mean and rolling standard deviation
- month number and quarter
- sine and cosine month encodings
- non-zero lag count across the previous 3 months

## 3. Train Test Protocol And Baseline

Explain that the split is chronological to prevent temporal leakage. Training uses months before the holdout start, while testing uses the final holdout months. The baseline is a naive persistence forecast where next-month demand equals the previous month's quantity.

## 4. Model Development

Describe the two candidate algorithms:

- Random Forest Regressor
- HistGradientBoostingRegressor

Mention that the target is log-transformed during training to reduce the effect of extreme demand spikes and then converted back to the original unit scale for evaluation. If you run `-EnableSearch`, mention the small randomized hyperparameter search over tree depth, leaf size, learning rate, and iteration count.

## 5. Performance Evaluation And Validation

Pull the exact numbers from `metrics_summary.csv`. Discuss:

- which model achieved the lowest MAE
- which model achieved the lowest RMSE
- whether the best model outperformed the naive baseline
- why `wmape` and `positive_only_mape` are more defensible than classical MAPE for zero-heavy demand

Suggested language:

`The holdout results show that the champion model outperformed the naive lag baseline on absolute error and weighted percentage error, but the remaining error level indicates substantial volatility and limited historical depth in the dataset.`

## 6. Interpretation, Risks, And Monitoring Hooks

Use `champion_feature_importance.csv` to describe the most influential predictors. The likely interpretation is that recent demand history and seasonal position dominate prediction quality.

State the main risks:

- short history window with only seven months of data
- strong zero inflation and irregular item demand
- possible concept drift across projects and construction phases
- inconsistent raw coding of items and units

Recommended monitoring hooks:

- rolling monthly MAE and WMAPE
- share of unseen item groups appearing in new data
- drift in lag-feature distributions
- retraining trigger when WMAPE materially worsens for two consecutive months

## 7. Figures To Insert

- `model_comparison.png`
- `champion_actual_vs_predicted.png`
- `champion_residual_plot.png`
- optionally a short table from `data_quality_summary.csv`

## 8. Honest Conclusion

Avoid claiming production readiness if the holdout metrics are weak. A stronger conclusion is:

`The workflow is production-structured and leakage-aware, but current predictive accuracy is not yet strong enough for high-confidence procurement commitments. Additional history, better item master-data governance, and clearer demand segmentation are needed before deployment.`
