# Presentation Outline

Use this for the stakeholder presentation version of the assignment.

## Slide 1: Title And Objective

- Procurement Demand Forecasting Workflow
- Objective: estimate next-month material demand for better procurement planning

## Slide 2: Business Problem

- current process is reactive and vulnerable to rush purchases
- forecast quality matters for bulk buying and supplier planning

## Slide 3: Data Overview

- weekly procurement sheets covering September 2025 to March 2026
- key fields: date, location, item, UOM, quantity received

## Slide 4: Data Preparation

- corrected date mismatches
- standardized item and UOM labels
- aggregated to monthly demand
- created lag and rolling features

## Slide 5: Modeling Approach

- naive lag baseline
- Random Forest
- HistGradientBoosting
- time-aware holdout to avoid leakage

## Slide 6: Model Results

- insert the model comparison table from `metrics_summary.csv`
- highlight the best model against the baseline

## Slide 7: Visual Result

- insert `model_comparison.png`
- explain which metric matters most for the business

## Slide 8: Prediction Quality View

- insert `champion_actual_vs_predicted.png`
- explain where the model tracks demand and where it misses

## Slide 9: Risks And Ethics

- limited history
- unstable percentage metrics under zero-demand months
- need for transparent assumptions and no overstated claims

## Slide 10: Operational Implications

- workflow is reusable and audit-friendly
- current performance supports analysis, not blind automation
- next improvement areas: more history, stronger item master data, retraining monitoring
