import { useCallback, useRef, type CSSProperties, type PointerEvent as ReactPointerEvent } from "react";
import { LAYOUT, stagePointFromEvent } from "../lib/layout";
import {
  buildSnakePaths,
  GENERATED_SNAKE_V2_ACTIVE_PATH_D,
  GENERATED_SNAKE_V2_COMPLETED_PATH_D,
  GENERATED_SNAKE_V2_LOCKED_PATH_D,
  GENERATED_SNAKE_V2_PATH_D,
  GENERATED_SNAKE_V2_POINTS,
  GENERATED_SNAKE_V2_RAW_PATH_D,
  SNAKE_CONFIG,
  type PathPoint,
} from "../lib/snake";
import {
  GENERATED_SNAKE_PATH_D,
  GENERATED_SNAKE_COMPLETED_PATH_D,
  GENERATED_SNAKE_ACTIVE_PATH_D,
  GENERATED_SNAKE_LOCKED_PATH_D,
  GENERATED_SNAKE_POINTS,
  GENERATED_SNAKE_WIDTH,
} from "../lib/snakePath.generated";

interface Props {
  pathPoints: PathPoint[];
  showPoints: boolean;
  onPathPointsChange: (points: PathPoint[]) => void;
  useGeneratedPath: boolean;
  useGeneratedPathV2: boolean;
  showRawV2Path: boolean;
  showExtractedPoints: boolean;
  showSkeletonOverlay: boolean;
  showMaskOverlay: boolean;
  showPathHandles: boolean;
  pathCalibrationMode: boolean;
}

export default function LessonPath({
  pathPoints,
  showPoints,
  onPathPointsChange,
  useGeneratedPath,
  useGeneratedPathV2,
  showRawV2Path,
  showExtractedPoints,
  showSkeletonOverlay,
  showMaskOverlay,
  showPathHandles,
  pathCalibrationMode,
}: Props) {
  const paths = buildSnakePaths(pathPoints);
  const dragRef = useRef<{ index: number; pointKey: "anchor" | "c1" | "c2" } | null>(null);

  const onPointerDown = useCallback(
    (index: number, pointKey: "anchor" | "c1" | "c2", event: ReactPointerEvent<SVGCircleElement>) => {
      dragRef.current = { index, pointKey };
      event.currentTarget.setPointerCapture(event.pointerId);
      event.currentTarget.classList.add("dragging");
    },
    [],
  );

  const onPointerMove = useCallback(
    (index: number, pointKey: "anchor" | "c1" | "c2", event: ReactPointerEvent<SVGCircleElement>) => {
      if (!dragRef.current || dragRef.current.index !== index || dragRef.current.pointKey !== pointKey) {
        return;
      }
      const nextPosition = stagePointFromEvent(event);
      onPathPointsChange(
        pathPoints.map((point, pointIndex) => {
          if (pointIndex !== index) {
            return point;
          }
          if (pointKey === "anchor") {
            return { ...point, ...nextPosition };
          }
          return { ...point, [pointKey]: nextPosition };
        }),
      );
    },
    [pathPoints, onPathPointsChange],
  );

  const onPointerUp = useCallback((event: ReactPointerEvent<SVGCircleElement>) => {
    dragRef.current = null;
    event.currentTarget.classList.remove("dragging");
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
  }, []);

  const activeCompletedD = useGeneratedPathV2 ? GENERATED_SNAKE_V2_COMPLETED_PATH_D : useGeneratedPath ? GENERATED_SNAKE_COMPLETED_PATH_D : paths.completedPath;
  const activeActiveD = useGeneratedPathV2 ? GENERATED_SNAKE_V2_ACTIVE_PATH_D : useGeneratedPath ? GENERATED_SNAKE_ACTIVE_PATH_D : paths.activePath;
  const activeLockedD = useGeneratedPathV2 ? GENERATED_SNAKE_V2_LOCKED_PATH_D : useGeneratedPath ? GENERATED_SNAKE_LOCKED_PATH_D : paths.lockedPath;
  const activeFullD = useGeneratedPathV2 ? GENERATED_SNAKE_V2_PATH_D : useGeneratedPath ? GENERATED_SNAKE_PATH_D : paths.fullPath;
  const snakeWidth = useGeneratedPath ? GENERATED_SNAKE_WIDTH : 13;
  const glowWidth = snakeWidth + 8;
  const extractedPoints = useGeneratedPathV2 ? GENERATED_SNAKE_V2_POINTS : GENERATED_SNAKE_POINTS;
  const pathStyle = {
    "--snake-body-width": `${snakeWidth}px`,
    "--snake-glow-width": `${glowWidth}px`,
    "--snake-shadow-width": `${Math.max(glowWidth + 2, snakeWidth + 12)}px`,
    "--snake-highlight-width": `${Math.max(2, snakeWidth * 0.16)}px`,
  } as CSSProperties;
  const showManualHandles = (showPoints || showPathHandles || pathCalibrationMode) && !useGeneratedPath && !useGeneratedPathV2;

  return (
    <>
      <svg className="lesson-path" viewBox="0 0 1086 1448" aria-hidden="true" style={pathStyle}>
        <defs>
          <filter id="snakeGlow" x="-20%" y="-20%" width="140%" height="140%">
            <feGaussianBlur stdDeviation={useGeneratedPathV2 ? SNAKE_CONFIG.glowBlurStdDeviation : 11} result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <filter id="snakeOrganicGlow" x="-28%" y="-28%" width="156%" height="156%">
            <feTurbulence type="fractalNoise" baseFrequency="0.015 0.036" numOctaves="2" seed="31" result="organicNoise" />
            <feDisplacementMap in="SourceGraphic" in2="organicNoise" scale="2.6" xChannelSelector="R" yChannelSelector="B" result="organicWarp" />
            <feGaussianBlur in="organicWarp" stdDeviation="3.6" result="organicBlur" />
            <feMerge>
              <feMergeNode in="organicBlur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <filter id="snakeShadow" x="-20%" y="-20%" width="140%" height="140%">
            <feDropShadow
              dx="0"
              dy={useGeneratedPathV2 ? SNAKE_CONFIG.shadowOffsetY : 12}
              stdDeviation={useGeneratedPathV2 ? SNAKE_CONFIG.shadowBlurStdDeviation : 12}
              floodColor="var(--bg-deep)"
              floodOpacity={useGeneratedPathV2 ? SNAKE_CONFIG.shadowOpacity : 0.6}
            />
          </filter>
          <filter id="snakeSmoke" x="-24%" y="-24%" width="148%" height="148%">
            <feTurbulence type="fractalNoise" baseFrequency="0.012 0.032" numOctaves="2" seed="18" result="noise" />
            <feDisplacementMap in="SourceGraphic" in2="noise" scale="4" xChannelSelector="R" yChannelSelector="G" result="warped" />
            <feGaussianBlur in="warped" stdDeviation="4.5" result="smokeBlur" />
            <feColorMatrix
              in="smokeBlur"
              type="matrix"
              values="0 0 0 0 0.02  0 0 0 0 0.93  0 0 0 0 0.78  0 0 0 0.72 0"
              result="smokeColor"
            />
            <feMerge>
              <feMergeNode in="smokeColor" />
            </feMerge>
          </filter>
          <filter id="snakeSparkle" x="-20%" y="-20%" width="140%" height="140%">
            <feGaussianBlur stdDeviation="0.9" result="sparkleBlur" />
            <feMerge>
              <feMergeNode in="sparkleBlur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <filter id="snakeLiquid" x="-18%" y="-18%" width="136%" height="136%">
            <feTurbulence type="fractalNoise" baseFrequency="0.018 0.045" numOctaves="2" seed="9" result="liquidNoise" />
            <feDisplacementMap in="SourceGraphic" in2="liquidNoise" scale="1" xChannelSelector="R" yChannelSelector="G" result="liquidWarp" />
            <feGaussianBlur in="liquidWarp" stdDeviation="0.16" result="softLiquid" />
            <feMerge>
              <feMergeNode in="softLiquid" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <filter id="snakeFlare" x="-120%" y="-120%" width="340%" height="340%">
            <feGaussianBlur stdDeviation="5.5" result="flareBlur" />
            <feMerge>
              <feMergeNode in="flareBlur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <filter id="nodeEnergyBloom" x="-85%" y="-85%" width="270%" height="270%">
            <feTurbulence type="fractalNoise" baseFrequency="0.022 0.04" numOctaves="2" seed="44" result="nodeNoise" />
            <feDisplacementMap in="SourceGraphic" in2="nodeNoise" scale="3.5" xChannelSelector="R" yChannelSelector="G" result="nodeWarp" />
            <feGaussianBlur in="nodeWarp" stdDeviation="5" result="nodeBlur" />
            <feMerge>
              <feMergeNode in="nodeBlur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <linearGradient id="snakeActive" x1="0" y1="360" x2="0" y2="760" gradientUnits="userSpaceOnUse">
            <stop stopColor="#0de1ae" />
            <stop offset="0.18" stopColor="#03a577" />
            <stop offset="0.36" stopColor="#20ffd0" />
            <stop offset="0.58" stopColor="#028061" />
            <stop offset="0.78" stopColor="#06c99a" />
            <stop offset="1" stopColor="#1df4c7" />
          </linearGradient>
          <linearGradient id="snakeLocked" x1="0" y1="760" x2="0" y2="1190" gradientUnits="userSpaceOnUse">
            <stop stopColor="#7bb1bc" stopOpacity="0.72" />
            <stop offset="0.22" stopColor="#2f5964" stopOpacity="0.84" />
            <stop offset="0.48" stopColor="#8fc6cf" stopOpacity="0.58" />
            <stop offset="0.72" stopColor="#314f5a" stopOpacity="0.86" />
            <stop offset="1" stopColor="#6e8795" stopOpacity="0.76" />
          </linearGradient>
          <linearGradient id="snakeSparkleGradient" x1="0" y1="360" x2="0" y2="830" gradientUnits="userSpaceOnUse">
            <stop stopColor="#f5fffb" stopOpacity="0.9" />
            <stop offset="0.18" stopColor="#86ffe3" stopOpacity="0.34" />
            <stop offset="0.43" stopColor="#ffffff" stopOpacity="0.72" />
            <stop offset="0.67" stopColor="#30ffd1" stopOpacity="0.3" />
            <stop offset="1" stopColor="#f2fffb" stopOpacity="0.64" />
          </linearGradient>
          <radialGradient id="nodeBloomActive" cx="45%" cy="38%" r="62%">
            <stop stopColor="#edfff9" stopOpacity="0.46" />
            <stop offset="0.28" stopColor="#1fffd0" stopOpacity="0.3" />
            <stop offset="0.64" stopColor="#00c79f" stopOpacity="0.11" />
            <stop offset="1" stopColor="#00c79f" stopOpacity="0" />
          </radialGradient>
          <radialGradient id="nodeBloomLocked" cx="44%" cy="38%" r="64%">
            <stop stopColor="#d8f4f6" stopOpacity="0.24" />
            <stop offset="0.36" stopColor="#8eb8c4" stopOpacity="0.17" />
            <stop offset="1" stopColor="#7ea8b5" stopOpacity="0" />
          </radialGradient>
        </defs>

        {showMaskOverlay && (
          <image
            href={useGeneratedPathV2 ? "/assets/debug/snake-mask-v2.png" : "/assets/debug/snake-mask-clean.png"}
            x="0"
            y="0"
            width="1086"
            height="1448"
            className="snake-mask-overlay"
          />
        )}

        {showSkeletonOverlay && (
          <image
            href={useGeneratedPathV2 ? "/assets/debug/snake-skeleton-v2.png" : "/assets/debug/snake-skeleton.png"}
            x="0"
            y="0"
            width="1086"
            height="1448"
            className="snake-skeleton-overlay"
          />
        )}

        <path
          className="path-shadow"
          d={activeFullD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.shadowWidth, opacity: SNAKE_CONFIG.shadowOpacity } : undefined}
        />
        <path
          className="path-depth completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.activeBodyWidth + 5, opacity: 0.48 } : undefined}
        />
        <path
          className="path-depth active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.activeBodyWidth + 5, opacity: 0.44 } : undefined}
        />
        <path
          className="path-depth locked"
          d={activeLockedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.lockedBodyWidth + 5, opacity: 0.42 } : undefined}
        />
        <g className="node-liquid-blooms" aria-hidden="true">
          <circle className="node-bloom completed" cx={LAYOUT.nodes.hello.x} cy={LAYOUT.nodes.hello.y} r="42" />
          <circle className="node-bloom completed" cx={LAYOUT.nodes.read.x} cy={LAYOUT.nodes.read.y} r="42" />
          <circle className="node-bloom current" cx={LAYOUT.nodes.strings.x} cy={LAYOUT.nodes.strings.y} r="58" />
          <circle className="node-bloom locked" cx={LAYOUT.nodes.functions.x} cy={LAYOUT.nodes.functions.y} r="38" />
          <circle className="node-bloom locked" cx={LAYOUT.nodes.compare.x} cy={LAYOUT.nodes.compare.y} r="38" />
          <circle className="node-bloom locked" cx={LAYOUT.nodes.mini.x} cy={LAYOUT.nodes.mini.y} r="38" />
          <circle className="node-bloom locked" cx={LAYOUT.nodes.cli.x} cy={LAYOUT.nodes.cli.y} r="38" />
        </g>
        <path
          className="path-smoke completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.smokeWidth, opacity: SNAKE_CONFIG.smokeOpacity } : undefined}
        />
        <path
          className="path-smoke active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.smokeWidth, opacity: SNAKE_CONFIG.smokeOpacity } : undefined}
        />
        <path
          className="path-smoke locked"
          d={activeLockedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.smokeWidth - 6, opacity: SNAKE_CONFIG.smokeOpacity * 0.55 } : undefined}
        />
        <path
          className="path-glow completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.activeGlowWidth, opacity: SNAKE_CONFIG.activeGlowOpacity } : undefined}
        />
        <path
          className="path-glow active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.activeGlowWidth, opacity: SNAKE_CONFIG.activeGlowOpacity } : undefined}
        />
        <path
          className="path-glow locked"
          d={activeLockedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.lockedGlowWidth, opacity: SNAKE_CONFIG.lockedGlowOpacity } : undefined}
        />
        <path
          className="path-body completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.activeBodyWidth, opacity: SNAKE_CONFIG.activeBodyOpacity } : undefined}
        />
        <path
          className="path-body active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.activeBodyWidth, opacity: SNAKE_CONFIG.activeBodyOpacity } : undefined}
        />
        <path
          className="path-body locked"
          d={activeLockedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.lockedBodyWidth, opacity: SNAKE_CONFIG.lockedBodyOpacity } : undefined}
        />
        <path
          className="path-edge completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.activeBodyWidth + 1.8, opacity: 0.42 } : undefined}
        />
        <path
          className="path-edge active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.activeBodyWidth + 1.6, opacity: 0.38 } : undefined}
        />
        <path
          className="path-edge locked"
          d={activeLockedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.lockedBodyWidth + 1.6, opacity: 0.24 } : undefined}
        />
        <path
          className="path-core-light completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: 3.6, opacity: 0.22 } : undefined}
        />
        <path
          className="path-core-light active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: 3.2, opacity: 0.18 } : undefined}
        />
        <path
          className="path-sparkle completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.sparkleWidth, opacity: SNAKE_CONFIG.sparkleOpacity } : undefined}
        />
        <path
          className="path-sparkle active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.sparkleWidth, opacity: SNAKE_CONFIG.sparkleOpacity * 0.78 } : undefined}
        />
        <path
          className="path-sparkle locked"
          d={activeLockedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.sparkleWidth - 1.4, opacity: SNAKE_CONFIG.sparkleOpacity * 0.22 } : undefined}
        />
        <g className="snake-flares" aria-hidden="true">
          <circle className="snake-flare hot" cx="303" cy="456" r="3.4" />
          <circle className="snake-flare hot" cx="344" cy="548" r="2.7" />
          <circle className="snake-flare hot" cx="420" cy="654" r="3.1" />
          <circle className="snake-flare soft" cx="385" cy="746" r="2.8" />
          <circle className="snake-flare dim" cx="319" cy="830" r="2.2" />
          <circle className="snake-flare dim" cx="412" cy="982" r="2.4" />
        </g>
        {showRawV2Path && (
          <path
            d={GENERATED_SNAKE_V2_RAW_PATH_D}
            fill="none"
            stroke="#ff4a68"
            strokeWidth="3.5"
            strokeDasharray="8,6"
            opacity="0.9"
            style={{ pointerEvents: "none" }}
          />
        )}
        <path
          className="path-highlight completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.highlightWidth, opacity: SNAKE_CONFIG.highlightOpacity } : undefined}
        />
        <path
          className="path-highlight active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: SNAKE_CONFIG.highlightWidth, opacity: SNAKE_CONFIG.highlightOpacity } : undefined}
        />
        <path
          className="path-highlight locked"
          d={activeLockedD}
          style={useGeneratedPathV2 ? { strokeWidth: Math.max(1.5, SNAKE_CONFIG.highlightWidth - 0.4), opacity: SNAKE_CONFIG.highlightOpacity * 0.26 } : undefined}
        />

        {showExtractedPoints && (
          <g className="snake-extracted-points">
            {extractedPoints.map((point, index) => (
              <circle
                key={`extracted-${index}`}
                cx={point.x}
                cy={point.y}
                r="4"
                className="extracted-point"
              />
            ))}
          </g>
        )}

      </svg>

      {showManualHandles && (
        <svg
          className="lesson-path path-handles-layer"
          viewBox="0 0 1086 1448"
          aria-hidden="true"
          style={{ zIndex: 70 }}
        >
          <g className="path-edit-points">
            {pathPoints.map((point, index) => (
              <g key={point.id}>
                {point.c1 && <line x1={point.x} y1={point.y} x2={point.c1.x} y2={point.c1.y} />}
                {point.c2 && <line x1={point.x} y1={point.y} x2={point.c2.x} y2={point.c2.y} />}
                {point.c1 && (
                  <circle
                    className="control"
                    cx={point.c1.x}
                    cy={point.c1.y}
                    r="8"
                    onPointerDown={(event) => onPointerDown(index, "c1", event)}
                    onPointerMove={(event) => onPointerMove(index, "c1", event)}
                    onPointerUp={onPointerUp}
                    onPointerCancel={onPointerUp}
                    onLostPointerCapture={onPointerUp}
                  />
                )}
                {point.c2 && (
                  <circle
                    className="control"
                    cx={point.c2.x}
                    cy={point.c2.y}
                    r="8"
                    onPointerDown={(event) => onPointerDown(index, "c2", event)}
                    onPointerMove={(event) => onPointerMove(index, "c2", event)}
                    onPointerUp={onPointerUp}
                    onPointerCancel={onPointerUp}
                    onLostPointerCapture={onPointerUp}
                  />
                )}
                <circle className="node-center" cx={point.x} cy={point.y} r="3.5" />
                <circle
                  className="anchor"
                  cx={point.x}
                  cy={point.y}
                  r="10"
                  onPointerDown={(event) => onPointerDown(index, "anchor", event)}
                  onPointerMove={(event) => onPointerMove(index, "anchor", event)}
                  onPointerUp={onPointerUp}
                  onPointerCancel={onPointerUp}
                  onLostPointerCapture={onPointerUp}
                />
              </g>
            ))}
          </g>
        </svg>
      )}

      {showExtractedPoints && (
        <svg
          className="lesson-path path-handles-layer"
          viewBox="0 0 1086 1448"
          aria-hidden="true"
          style={{ zIndex: 60 }}
        >
          {extractedPoints.map((point, index) => (
            <g key={`handle-${index}`}>
              <circle
                className="anchor generated-handle"
                cx={point.x}
                cy={point.y}
                r="10"
              />
              <circle
                className="node-center"
                cx={point.x}
                cy={point.y}
                r="3.5"
              />
            </g>
          ))}
        </svg>
      )}
    </>
  );
}
