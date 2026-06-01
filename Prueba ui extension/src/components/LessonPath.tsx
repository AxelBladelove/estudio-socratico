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
          <filter id="snakeTightBloom" x="-28%" y="-28%" width="156%" height="156%">
            <feGaussianBlur stdDeviation="1.85" result="tightBloom" />
            <feMerge>
              <feMergeNode in="tightBloom" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <filter id="snakeOrganicGlow" x="-28%" y="-28%" width="156%" height="156%">
            <feTurbulence type="fractalNoise" baseFrequency="0.015 0.036" numOctaves="2" seed="31" result="organicNoise" />
            <feDisplacementMap in="SourceGraphic" in2="organicNoise" scale="1.45" xChannelSelector="R" yChannelSelector="B" result="organicWarp" />
            <feGaussianBlur in="organicWarp" stdDeviation="2.7" result="organicBlur" />
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
          <filter id="snakeSpecularCut" x="-18%" y="-18%" width="136%" height="136%">
            <feGaussianBlur stdDeviation="0.32" result="specularSoft" />
            <feDropShadow dx="0" dy="0" stdDeviation="1.6" floodColor="#66ffe4" floodOpacity="0.55" />
            <feMerge>
              <feMergeNode in="specularSoft" />
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
            <feGaussianBlur stdDeviation="3.6" result="flareBlur" />
            <feMerge>
              <feMergeNode in="flareBlur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
          <filter id="snakeContactBloom" x="-90%" y="-90%" width="280%" height="280%">
            <feTurbulence type="fractalNoise" baseFrequency="0.03 0.052" numOctaves="2" seed="62" result="contactNoise" />
            <feDisplacementMap in="SourceGraphic" in2="contactNoise" scale="2.2" xChannelSelector="R" yChannelSelector="G" result="contactWarp" />
            <feGaussianBlur in="contactWarp" stdDeviation="4.2" result="contactBlur" />
            <feMerge>
              <feMergeNode in="contactBlur" />
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
            <stop stopColor="#9af6db" />
            <stop offset="0.1" stopColor="#12886b" />
            <stop offset="0.22" stopColor="#063a34" />
            <stop offset="0.34" stopColor="#6ff0cc" />
            <stop offset="0.48" stopColor="#0c5b4e" />
            <stop offset="0.62" stopColor="#06252d" />
            <stop offset="0.76" stopColor="#29c59c" />
            <stop offset="0.9" stopColor="#0a5147" />
            <stop offset="1" stopColor="#d9fff4" />
          </linearGradient>
          <linearGradient id="snakeRimActive" x1="0" y1="360" x2="0" y2="760" gradientUnits="userSpaceOnUse">
            <stop stopColor="#f5fffb" stopOpacity="0.52" />
            <stop offset="0.12" stopColor="#89fff0" stopOpacity="0.18" />
            <stop offset="0.31" stopColor="#d7fff8" stopOpacity="0.58" />
            <stop offset="0.5" stopColor="#0a1d26" stopOpacity="0.14" />
            <stop offset="0.68" stopColor="#5eead2" stopOpacity="0.18" />
            <stop offset="0.84" stopColor="#ffffff" stopOpacity="0.44" />
            <stop offset="1" stopColor="#9de6df" stopOpacity="0.28" />
          </linearGradient>
          <linearGradient id="snakeLocked" x1="0" y1="760" x2="0" y2="1190" gradientUnits="userSpaceOnUse">
            <stop stopColor="#81939a" stopOpacity="0.76" />
            <stop offset="0.16" stopColor="#33434b" stopOpacity="0.94" />
            <stop offset="0.34" stopColor="#182932" stopOpacity="0.98" />
            <stop offset="0.52" stopColor="#9ab1b7" stopOpacity="0.56" />
            <stop offset="0.72" stopColor="#2a3a43" stopOpacity="0.94" />
            <stop offset="0.88" stopColor="#14242e" stopOpacity="0.98" />
            <stop offset="1" stopColor="#667a84" stopOpacity="0.82" />
          </linearGradient>
          <linearGradient id="snakeRimLocked" x1="0" y1="760" x2="0" y2="1190" gradientUnits="userSpaceOnUse">
            <stop stopColor="#eefcfd" stopOpacity="0.24" />
            <stop offset="0.36" stopColor="#97e8e9" stopOpacity="0.12" />
            <stop offset="0.68" stopColor="#d5f4f5" stopOpacity="0.18" />
            <stop offset="1" stopColor="#7edee1" stopOpacity="0.1" />
          </linearGradient>
          <linearGradient id="snakeSparkleGradient" x1="0" y1="360" x2="0" y2="830" gradientUnits="userSpaceOnUse">
            <stop stopColor="#f5fffb" stopOpacity="0.9" />
            <stop offset="0.18" stopColor="#86ffe3" stopOpacity="0.34" />
            <stop offset="0.43" stopColor="#ffffff" stopOpacity="0.72" />
            <stop offset="0.67" stopColor="#30ffd1" stopOpacity="0.3" />
            <stop offset="1" stopColor="#f2fffb" stopOpacity="0.64" />
          </linearGradient>
          <linearGradient id="snakeInnerShade" x1="0" y1="360" x2="0" y2="1190" gradientUnits="userSpaceOnUse">
            <stop stopColor="#001815" stopOpacity="0.45" />
            <stop offset="0.24" stopColor="#00382f" stopOpacity="0.18" />
            <stop offset="0.52" stopColor="#000d14" stopOpacity="0.34" />
            <stop offset="0.8" stopColor="#132b35" stopOpacity="0.28" />
            <stop offset="1" stopColor="#020a10" stopOpacity="0.42" />
          </linearGradient>
          <linearGradient id="snakeSpecularGradient" x1="220" y1="380" x2="500" y2="1130" gradientUnits="userSpaceOnUse">
            <stop stopColor="#ffffff" stopOpacity="0.94" />
            <stop offset="0.28" stopColor="#cffff7" stopOpacity="0.86" />
            <stop offset="0.52" stopColor="#4dffdf" stopOpacity="0.36" />
            <stop offset="0.74" stopColor="#eefcff" stopOpacity="0.62" />
            <stop offset="1" stopColor="#b8e4eb" stopOpacity="0.42" />
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
          <radialGradient id="snakeContactActive" cx="50%" cy="50%" r="58%">
            <stop stopColor="#ffffff" stopOpacity="0.5" />
            <stop offset="0.18" stopColor="#55ffdf" stopOpacity="0.32" />
            <stop offset="0.62" stopColor="#00d0a3" stopOpacity="0.08" />
            <stop offset="1" stopColor="#00d0a3" stopOpacity="0" />
          </radialGradient>
          <radialGradient id="snakeContactCurrent" cx="50%" cy="50%" r="62%">
            <stop stopColor="#f7fffd" stopOpacity="0.68" />
            <stop offset="0.24" stopColor="#55ffdf" stopOpacity="0.44" />
            <stop offset="0.68" stopColor="#00d6cc" stopOpacity="0.12" />
            <stop offset="1" stopColor="#00d6cc" stopOpacity="0" />
          </radialGradient>
          <radialGradient id="snakeContactLocked" cx="50%" cy="50%" r="58%">
            <stop stopColor="#e6fbff" stopOpacity="0.26" />
            <stop offset="0.32" stopColor="#8ed2dc" stopOpacity="0.14" />
            <stop offset="1" stopColor="#8ed2dc" stopOpacity="0" />
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
        <g className="snake-connector-blooms" aria-hidden="true">
          <ellipse className="contact active" cx="290" cy="432" rx="15" ry="27" transform="rotate(-18 290 432)" />
          <ellipse className="contact active" cx="348" cy="491" rx="17" ry="25" transform="rotate(-34 348 491)" />
          <ellipse className="contact current" cx="420" cy="625" rx="24" ry="34" transform="rotate(-18 420 625)" />
          <ellipse className="contact current exit" cx="397" cy="721" rx="20" ry="31" transform="rotate(42 397 721)" />
          <ellipse className="contact locked" cx="331" cy="784" rx="17" ry="25" transform="rotate(28 331 784)" />
          <ellipse className="contact locked" cx="357" cy="909" rx="14" ry="22" transform="rotate(-18 357 909)" />
          <ellipse className="contact locked" cx="441" cy="1019" rx="14" ry="22" transform="rotate(-24 441 1019)" />
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
          className="path-inner-shade completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: 5.2, opacity: 0.38 } : undefined}
        />
        <path
          className="path-inner-shade active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: 4.8, opacity: 0.32 } : undefined}
        />
        <path
          className="path-inner-shade locked"
          d={activeLockedD}
          style={useGeneratedPathV2 ? { strokeWidth: 4.4, opacity: 0.28 } : undefined}
        />
        <path
          className="path-rim completed"
          d={activeCompletedD}
          style={useGeneratedPathV2 ? { strokeWidth: 5.2, opacity: 0.5 } : undefined}
        />
        <path
          className="path-rim active"
          d={activeActiveD}
          style={useGeneratedPathV2 ? { strokeWidth: 4.8, opacity: 0.42 } : undefined}
        />
        <path
          className="path-rim locked"
          d={activeLockedD}
          style={useGeneratedPathV2 ? { strokeWidth: 3.8, opacity: 0.26 } : undefined}
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
          <circle className="snake-flare hot" cx="300" cy="454" r="2.2" />
          <circle className="snake-flare hot" cx="363" cy="514" r="2.4" />
          <circle className="snake-flare hot" cx="427" cy="642" r="2.6" />
          <circle className="snake-flare soft" cx="386" cy="735" r="2.1" />
          <circle className="snake-flare dim" cx="322" cy="824" r="1.8" />
          <circle className="snake-flare dim" cx="410" cy="978" r="1.9" />
          <circle className="snake-flare dim" cx="426" cy="1096" r="1.7" />
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
