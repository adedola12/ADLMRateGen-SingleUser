# Run Summary

These values came from the verified test run of `procurement_forecast_pipeline.py`.

## Holdout Setup

- Training months: September 2025 to January 2026
- Test months: February 2026 to March 2026
- Unit of analysis: monthly demand by `Location + Item Group + UOM`

## Model Results

| Model | MAE | RMSE | Safe MAPE | Positive-Only MAPE | WMAPE | R2 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| HistGradientBoosting | 68.23 | 524.61 | 184.04% | 87.50% | 95.06% | 0.135 |
| Random Forest | 71.31 | 558.54 | 163.24% | 91.64% | 99.34% | 0.019 |
| Naive Lag-1 Baseline | 98.18 | 685.08 | 3752.34% | 114.90% | 136.77% | -0.476 |

## Main Takeaways

- `HistGradientBoosting` was the champion model because it achieved the best MAE, RMSE, WMAPE, and R2 on the holdout set.
- Both machine-learning models clearly outperformed the naive lag baseline.
- Classical MAPE is unstable because the holdout contains many zero-demand rows, so `WMAPE` and `positive_only_mape` are safer to discuss in the report.
- The workflow is defensible and leakage-aware, but the final error levels show that the dataset is still too volatile and short for a strong production-readiness claim.

## Suggested Report Sentence

`The HistGradientBoosting model emerged as the best candidate, reducing MAE from 98.18 units in the naive baseline to 68.23 units and improving weighted MAPE from 136.77% to 95.06%. However, the remaining error indicates that the current dataset should be treated as an analytical forecasting prototype rather than a fully production-ready procurement decision engine.`
