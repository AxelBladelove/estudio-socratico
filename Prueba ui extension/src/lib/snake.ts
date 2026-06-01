import { LAYOUT } from "./layout";
import {
  GENERATED_SNAKE_V2_POINTS,
  GENERATED_SNAKE_V2_PATH_D,
  GENERATED_SNAKE_V2_RAW_PATH_D,
  GENERATED_SNAKE_V2_COMPLETED_PATH_D,
  GENERATED_SNAKE_V2_ACTIVE_PATH_D,
  GENERATED_SNAKE_V2_LOCKED_PATH_D,
  GENERATED_SNAKE_V2_WIDTH,
} from "./snakePathV2.generated";

export {
  GENERATED_SNAKE_V2_POINTS,
  GENERATED_SNAKE_V2_PATH_D,
  GENERATED_SNAKE_V2_RAW_PATH_D,
  GENERATED_SNAKE_V2_COMPLETED_PATH_D,
  GENERATED_SNAKE_V2_ACTIVE_PATH_D,
  GENERATED_SNAKE_V2_LOCKED_PATH_D,
  GENERATED_SNAKE_V2_WIDTH,
};

// Visual stroke calibration config for pixel-accurate styling
export const SNAKE_CONFIG = {
  // Stroke widths
  activeBodyWidth: 12,
  lockedBodyWidth: 10.6,
  activeGlowWidth: 15.8,
  lockedGlowWidth: 12.8,
  highlightWidth: 1.45,
  shadowWidth: 16.2,
  smokeWidth: 16.6,
  sparkleWidth: 2.2,

  // Opacities & Intensities
  activeBodyOpacity: 0.98,
  lockedBodyOpacity: 0.82,
  activeGlowOpacity: 0.12,
  lockedGlowOpacity: 0.06,
  highlightOpacity: 0.72,
  shadowOpacity: 0.68,
  smokeOpacity: 0.028,
  sparkleOpacity: 0.68,

  // Blur deviations
  glowBlurStdDeviation: 3.4,
  shadowBlurStdDeviation: 4.8,
  shadowOffsetY: 5,
};

export interface PathPoint {
  id: string;
  x: number;
  y: number;
  c1?: { x: number; y: number };
  c2?: { x: number; y: number };
}

export interface SnakePaths {
  completedPath: string;
  activePath: string;
  lockedPath: string;
  fullPath: string;
}

const PATH_STORAGE_KEY = "snake-path-points-v2";

export const SNAKE_PATH_POINTS: PathPoint[] = [
  { id: "hello", x: LAYOUT.nodes.hello.x, y: LAYOUT.nodes.hello.y },
  { id: "read", x: LAYOUT.nodes.read.x, y: LAYOUT.nodes.read.y, c1: { x: 277, y: 444 }, c2: { x: 292, y: 489 } },
  { id: "strings", x: LAYOUT.nodes.strings.x, y: LAYOUT.nodes.strings.y, c1: { x: 426, y: 552 }, c2: { x: 414, y: 614 } },
  { id: "functions", x: LAYOUT.nodes.functions.x, y: LAYOUT.nodes.functions.y, c1: { x: 456, y: 730 }, c2: { x: 356, y: 767 } },
  { id: "compare", x: LAYOUT.nodes.compare.x, y: LAYOUT.nodes.compare.y, c1: { x: 286, y: 860 }, c2: { x: 323, y: 902 } },
  { id: "mini", x: LAYOUT.nodes.mini.x, y: LAYOUT.nodes.mini.y, c1: { x: 430, y: 962 }, c2: { x: 462, y: 1008 } },
  { id: "cli", x: LAYOUT.nodes.cli.x, y: LAYOUT.nodes.cli.y, c1: { x: 444, y: 1112 }, c2: { x: 414, y: 1144 } },
];

function segmentTo(previous: PathPoint, point: PathPoint) {
  return `M ${previous.x} ${previous.y} C ${point.c1?.x ?? point.x} ${point.c1?.y ?? point.y} ${point.c2?.x ?? point.x} ${
    point.c2?.y ?? point.y
  } ${point.x} ${point.y}`;
}

export function buildSnakePathD(points: PathPoint[]) {
  const [first, ...rest] = points;
  if (!first) {
    return "";
  }
  return rest.reduce((path, point) => `${path} C ${point.c1?.x ?? point.x} ${point.c1?.y ?? point.y} ${point.c2?.x ?? point.x} ${point.c2?.y ?? point.y} ${point.x} ${point.y}`, `M ${first.x} ${first.y}`);
}

export function buildSnakePaths(points: PathPoint[]): SnakePaths {
  return {
    completedPath: points[0] && points[1] ? segmentTo(points[0], points[1]) : "",
    activePath: points[1] && points[2] ? segmentTo(points[1], points[2]) : "",
    lockedPath: points.length > 2 ? buildSnakePathD(points.slice(2)) : "",
    fullPath: buildSnakePathD(points),
  };
}

export function loadPathPoints() {
  try {
    const raw = window.localStorage.getItem(PATH_STORAGE_KEY);
    const points = raw ? (JSON.parse(raw) as PathPoint[]) : SNAKE_PATH_POINTS;
    return points.map((p) => {
      const node = LAYOUT.nodes[p.id as keyof typeof LAYOUT.nodes];
      if (node) {
        return { ...p, x: node.x, y: node.y };
      }
      return p;
    });
  } catch {
    return SNAKE_PATH_POINTS;
  }
}

export function savePathPoints(points: PathPoint[]) {
  window.localStorage.setItem(PATH_STORAGE_KEY, JSON.stringify(points));
}
