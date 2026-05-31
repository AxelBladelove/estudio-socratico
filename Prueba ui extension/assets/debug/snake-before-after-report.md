# Snake Path Before/After Report

Winner: **final-v2**

| Variant | Score | Mask IoU | Path->ref mean | Path->ref p95 | Ref->path mean | Ref->path p95 | Stroke |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| baseline-v1 | -54.824 | 0.15471 | 19.866 | 49.0 | 6.615 | 16.782 | 14.5 |
| final-v2 | -22.844 | 0.31407 | 17.728 | 49.166 | 0.776 | 2.8 | 12.5 |

Improvement deltas are final minus baseline:

- scoreDelta: 31.98
- maskIoUDelta: 0.15936
- pathMeanDeltaPx: -2.138
- refMeanDeltaPx: -5.839

Artifacts:
- PNG: `assets\debug\snake-before-after-comparison.png`
- SVG: `assets\debug\snake-before-after-comparison.svg`
- JSON: `assets\debug\snake-before-after-report.json`
