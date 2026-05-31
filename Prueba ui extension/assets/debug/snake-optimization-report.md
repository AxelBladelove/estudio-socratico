# Snake Optimization Report

Winner: **spacing-10_sigma-1_natural**
Final improves current V2: **True**

| Variant | Score | Rounded score | IoU | Ref->path mean | Ref->path p95 | Path->ref mean | Lower curv p95 | Lower spike count |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| baseline-v1 | -88.613 | -124.411 | 0.35973 | 6.765 | 16.861 | 6.022 | 6.366 | 2 |
| current-v2-before | 93.779 | 66.225 | 0.80266 | 0.591 | 1.4 | 0.496 | 3.293 | 8 |
| selected-final | 89.88 | 61.555 | 0.82102 | 0.805 | 2.8 | 0.956 | 3.748 | 6 |

Top trials:
- spacing-10_sigma-1_natural: rounded score 61.555, IoU 0.82102, ref mean 0.805px, lower curvature p95 3.748deg, lower spikes 6
- spacing-10_sigma-1_not-a-knot: rounded score 61.555, IoU 0.82102, ref mean 0.805px, lower curvature p95 3.748deg, lower spikes 6
- bottom-rounded_spacing-8_upper-1.25_lower-7_natural: rounded score 58.57, IoU 0.80798, ref mean 0.66px, lower curvature p95 4.142deg, lower spikes 7
- bottom-rounded_spacing-8_upper-1.25_lower-7_not-a-knot: rounded score 58.57, IoU 0.80798, ref mean 0.66px, lower curvature p95 4.142deg, lower spikes 7
- bottom-rounded_spacing-8_upper-1_lower-7_natural: rounded score 58.43, IoU 0.80716, ref mean 0.66px, lower curvature p95 4.142deg, lower spikes 7
- bottom-rounded_spacing-8_upper-1_lower-7_not-a-knot: rounded score 58.43, IoU 0.80716, ref mean 0.66px, lower curvature p95 4.142deg, lower spikes 7
- bottom-rounded_spacing-8_upper-0.85_lower-7_natural: rounded score 58.015, IoU 0.80815, ref mean 0.66px, lower curvature p95 4.142deg, lower spikes 7
- bottom-rounded_spacing-8_upper-0.85_lower-7_not-a-knot: rounded score 58.015, IoU 0.80815, ref mean 0.66px, lower curvature p95 4.142deg, lower spikes 7

Artifacts:
- `assets\debug\snake-optimization-before-after.png`
- `assets\debug\snake-rounded-ui-vs-reference-lower.png`
- `assets\debug\snake-optimization-trials.svg`
- `assets\debug\snake-optimization-report.json`
