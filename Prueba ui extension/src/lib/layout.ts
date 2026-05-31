import { useEffect, useState, type MouseEvent } from "react";

export const STAGE = { width: 1086, height: 1448 };

// DO NOT EDIT PATH IN THIS PASS.
// DO NOT EDIT COLORS IN THIS PASS.
// DO NOT EDIT EFFECTS IN THIS PASS.
export const LAYOUT = {
  header: {
    backButton: { x: 36, y: 49, w: 246, h: 70 },
    backIcon: { x: 25, y: 20, w: 31, h: 31 },
    backText: { x: 63, y: 20, fontSize: 25, lineHeight: 25, fontWeight: 400 },
    moduleLabel: { x: 0, y: 0, fontSize: 19, lineHeight: 19, fontWeight: 850 },
    title: { x: 0, y: 37, fontSize: 42, lineHeight: 42, fontWeight: 800 },
    subtitle: { x: 0, y: 88, fontSize: 26, lineHeight: 26, fontWeight: 400 },
    titleBlock: { x: 318, y: 38, w: 540, h: 112 },
    modulesButton: { x: 871, y: 55, w: 174, h: 65 },
    modulesIcon: { x: 25, y: 17, w: 31, h: 31 },
    modulesText: { x: 67, y: 18, fontSize: 24, lineHeight: 24, fontWeight: 400 },
  },
  progress: {
    card: { x: 59, y: 189, w: 968, h: 126 },
    leftNumber: { x: 28, y: 31, fontSize: 28, lineHeight: 31, fontWeight: 850 },
    leftRest: { x: 55, y: 36, fontSize: 22, lineHeight: 25, fontWeight: 400 },
    rightNumber: { x: 777, y: 31, fontSize: 28, lineHeight: 31, fontWeight: 850 },
    rightRest: { x: 832, y: 36, fontSize: 22, lineHeight: 25, fontWeight: 400 },
    progressSegments: { x: 29, y: 81, completedWidth: 76, pendingWidth: 62, height: 12, gap: 8, completedCount: 4, totalCount: 13 },
    segments: { x: 29, y: 81, w: 76, h: 12, gap: 8, count: 13 },
    segmentWidths: [76, 76, 76, 76, 15, 63, 62, 62, 62, 62, 62, 48, 69],
  },
  codeSnippet: { x: 30, y: 525, fontSize: 20, lineHeight: 34.8 },
  rightDecoration: { x: 620, y: 371, w: 380, h: 520 },
  bottomNav: {
    card: { x: 72, y: 1300, w: 940, h: 100 },
    leftIcon: { x: 36, y: 33, w: 39, h: 39 },
    nextText: { x: 112, y: 34, fontSize: 21, lineHeight: 26 },
    bottomNext: { x: 112, y: 34, fontSize: 21, lineHeight: 26, fontWeight: 700 },
    bottomLesson: { x: 220, y: 34, fontSize: 21, lineHeight: 26, fontWeight: 400 },
    arrowButton: { x: 853, y: 18, w: 63, h: 63 },
    arrowIcon: { x: 14, y: 14, w: 35, h: 35 },
  },
  nodes: {
    hello: { x: 288, y: 394, size: 72 },
    read: { x: 359, y: 520, size: 72 },
    strings: { x: 440, y: 671, size: 102 },
    functions: { x: 327, y: 815, size: 72 },
    compare: { x: 377, y: 935, size: 72 },
    mini: { x: 444, y: 1059, size: 72 },
    cli: { x: 405, y: 1178, size: 72 },
  },
  labels: {
    hello: { x: 358, y: 376 },
    read: { x: 422, y: 501 },
    strings: { x: 519, y: 648 },
    functions: { x: 397, y: 800 },
    compare: { x: 441, y: 920 },
    mini: { x: 511, y: 1036 },
    cli: { x: 484, y: 1154 },
  },
  lessonTexts: {
    hello: {
      title: { x: 358, y: 376, fontSize: 22, lineHeight: 24, fontWeight: 600 },
      subtitle: { x: 358, y: 412, fontSize: 20, lineHeight: 21, fontWeight: 400 },
    },
    read: {
      title: { x: 422, y: 501, fontSize: 22, lineHeight: 24, fontWeight: 600 },
      subtitle: { x: 422, y: 537, fontSize: 20, lineHeight: 21, fontWeight: 400 },
    },
    strings: {
      title: { x: 519, y: 650, fontSize: 24, lineHeight: 25, fontWeight: 600 },
      subtitle: { x: 519, y: 686, fontSize: 20, lineHeight: 21, fontWeight: 400 },
      cta: { x: 519, y: 719, w: 125, h: 34, fontSize: 15, lineHeight: 32, fontWeight: 850 },
    },
    functions: {
      title: { x: 397, y: 800, fontSize: 22, lineHeight: 24, fontWeight: 600 },
      subtitle: { x: 397, y: 836, fontSize: 20, lineHeight: 21, fontWeight: 400 },
    },
    compare: {
      title: { x: 441, y: 917, fontSize: 22, lineHeight: 24, fontWeight: 600 },
      subtitle: { x: 441, y: 952, fontSize: 20, lineHeight: 21, fontWeight: 400 },
    },
    mini: {
      title: { x: 511, y: 1034, fontSize: 22, lineHeight: 24, fontWeight: 600 },
      subtitle: { x: 511, y: 1070, fontSize: 20, lineHeight: 21, fontWeight: 400 },
    },
    cli: {
      title: { x: 484, y: 1151, fontSize: 22, lineHeight: 24, fontWeight: 600 },
      subtitle: { x: 484, y: 1187, fontSize: 20, lineHeight: 21, fontWeight: 400 },
    },
  },
  labelText: {
    title: { fontSize: 22, lineHeight: 23, fontWeight: 700 },
    subtitle: { fontSize: 20, lineHeight: 21 },
    currentTitle: { fontSize: 24, lineHeight: 25 },
    badge: { x: 0, y: 50, w: 125, h: 34, fontSize: 15, lineHeight: 32 },
  },
};

export type LessonStatus = "completed" | "current" | "locked" | "challenge";

export interface Lesson {
  id: keyof typeof LAYOUT.nodes;
  title: string;
  subtitle: string;
  status: LessonStatus;
  x: number;
  y: number;
  size: number;
  labelX: number;
  labelY: number;
}

const nodeLessonData: Array<Pick<Lesson, "id" | "title" | "subtitle" | "status">> = [
  { id: "hello", title: "Hola mundo", subtitle: "printf", status: "completed" },
  { id: "read", title: "Leer texto", subtitle: "scanf / fgets", status: "completed" },
  { id: "strings", title: "Strings básicos", subtitle: "char[], cadenas", status: "current" },
  { id: "functions", title: "Funciones string", subtitle: "strlen, strcpy", status: "locked" },
  { id: "compare", title: "Comparar cadenas", subtitle: "strcmp", status: "locked" },
  { id: "mini", title: "Mini reto", subtitle: "Validador de nombre", status: "challenge" },
  { id: "cli", title: "Reto: agenda CLI", subtitle: "strings + menú", status: "locked" },
];

export const LESSONS: Lesson[] = nodeLessonData.map((lesson) => ({
  ...lesson,
  x: LAYOUT.nodes[lesson.id].x,
  y: LAYOUT.nodes[lesson.id].y,
  size: LAYOUT.nodes[lesson.id].size,
  labelX: LAYOUT.labels[lesson.id].x,
  labelY: LAYOUT.labels[lesson.id].y,
}));

export interface PathPoint {
  id: string;
  x: number;
  y: number;
  c1?: { x: number; y: number };
  c2?: { x: number; y: number };
}

export const LESSON_PATH_POINTS: PathPoint[] = [
  { id: "hello", x: 288, y: 390 },
  { id: "read", x: 360, y: 515, c1: { x: 284, y: 444 }, c2: { x: 304, y: 486 } },
  { id: "strings", x: 440, y: 670, c1: { x: 414, y: 552 }, c2: { x: 416, y: 604 } },
  { id: "functions", x: 330, y: 815, c1: { x: 456, y: 724 }, c2: { x: 380, y: 768 } },
  { id: "compare", x: 378, y: 935, c1: { x: 294, y: 866 }, c2: { x: 334, y: 910 } },
  { id: "mini", x: 445, y: 1060, c1: { x: 424, y: 964 }, c2: { x: 458, y: 1006 } },
  { id: "cli", x: 405, y: 1180, c1: { x: 438, y: 1111 }, c2: { x: 420, y: 1148 } },
];

export function buildLessonPathD(points: PathPoint[]) {
  const [first, ...rest] = points;
  return rest.reduce(
    (path, point) =>
      `${path} C ${point.c1?.x ?? point.x} ${point.c1?.y ?? point.y} ${point.c2?.x ?? point.x} ${point.c2?.y ?? point.y} ${point.x} ${point.y}`,
    `M ${first.x} ${first.y}`,
  );
}

export const LESSON_PATH_D = buildLessonPathD(LESSON_PATH_POINTS);

export type CalibrationType = "text" | "box" | "svg" | "progress" | "node";

export type CalibrationElement = {
  type: CalibrationType;
  x: number;
  y: number;
  w?: number;
  h?: number;
  fontSize?: number;
  lineHeight?: number;
  fontWeight?: number;
  letterSpacing?: number;
  scaleX?: number;
  scaleY?: number;
  rotate?: number;
  fontFamily?: string;
  radius?: number;
};

const progressSegments = LAYOUT.progress.segmentWidths.map((width, index) => {
  const x = LAYOUT.progress.segmentWidths.slice(0, index).reduce((sum, current) => sum + current + LAYOUT.progress.progressSegments.gap, 0);
  return [
    `progress.segment.${String(index + 1).padStart(2, "0")}`,
    {
      type: "progress",
      x: LAYOUT.progress.progressSegments.x + x,
      y: LAYOUT.progress.progressSegments.y,
      w: width,
      h: LAYOUT.progress.progressSegments.height,
      radius: 999,
      scaleX: 1,
      scaleY: 1,
    },
  ] as const;
});

const extraProgressSegment = [
  "progress.segment.14",
  {
    type: "progress",
    x:
      LAYOUT.progress.progressSegments.x +
      LAYOUT.progress.segmentWidths.reduce((sum, current) => sum + current + LAYOUT.progress.progressSegments.gap, 0),
    y: LAYOUT.progress.progressSegments.y,
    w: 43,
    h: LAYOUT.progress.progressSegments.height,
    radius: 999,
    scaleX: 1,
    scaleY: 1,
  },
] as const;

const lessonCalibration = Object.fromEntries(
  LESSONS.flatMap((lesson) => {
    const text = LAYOUT.lessonTexts[lesson.id];
    const base = [
      [
        `node.${lesson.id}.box`,
        {
          type: "node",
          x: lesson.x - lesson.size / 2,
          y: lesson.y - lesson.size / 2,
          w: lesson.size,
          h: lesson.size,
          scaleX: 1,
          scaleY: 1,
        },
      ],
      [
        `node.${lesson.id}.icon`,
        {
          type: "svg",
          x: lesson.status === "locked" ? 16 : 14,
          y: lesson.status === "locked" ? 16 : 14,
          w: lesson.status === "locked" ? 39 : 43,
          h: lesson.status === "locked" ? 39 : 43,
          scaleX: 1,
          scaleY: 1,
          rotate: 0,
        },
      ],
      [
        `label.${lesson.id}.title`,
        {
          type: "text",
          ...text.title,
          letterSpacing: 0,
          scaleX: 1,
          scaleY: 1,
          fontFamily: "var(--font)",
        },
      ],
      [
        `label.${lesson.id}.subtitle`,
        {
          type: "text",
          ...text.subtitle,
          letterSpacing: 0,
          scaleX: 1,
          scaleY: 1,
          fontFamily: "var(--font)",
        },
      ],
    ] as Array<[string, CalibrationElement]>;

    if (lesson.id === "strings") {
      base.push(
        [
          "node.strings.sideDot",
          { type: "box", x: 99, y: 38, w: 25, h: 25, scaleX: 1, scaleY: 1 },
        ],
        [
          "cta.box",
          {
            type: "box",
            x: LAYOUT.lessonTexts.strings.cta.x,
            y: LAYOUT.lessonTexts.strings.cta.y,
            w: LAYOUT.lessonTexts.strings.cta.w,
            h: LAYOUT.lessonTexts.strings.cta.h,
            scaleX: 1,
            scaleY: 1,
          },
        ],
        [
          "cta.text",
          {
            type: "text",
            x: 0,
            y: 0,
            fontSize: LAYOUT.lessonTexts.strings.cta.fontSize,
            lineHeight: LAYOUT.lessonTexts.strings.cta.lineHeight,
            fontWeight: LAYOUT.lessonTexts.strings.cta.fontWeight,
            letterSpacing: 0,
            scaleX: 1,
            scaleY: 1,
            fontFamily: "var(--font)",
          },
        ],
      );
    }

    return base;
  }),
) as Record<string, CalibrationElement>;

export const CALIBRATION_LAYOUT = {
  elements: {
    "backButton.box": { type: "box", ...LAYOUT.header.backButton, scaleX: 1, scaleY: 1 },
    "backButton.icon": { type: "svg", ...LAYOUT.header.backIcon, scaleX: 1, scaleY: 1, rotate: 0 },
    "backButton.text": {
      type: "text",
      ...LAYOUT.header.backText,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "moduleLabel.text": {
      type: "text",
      x: LAYOUT.header.titleBlock.x + LAYOUT.header.moduleLabel.x,
      y: LAYOUT.header.titleBlock.y + LAYOUT.header.moduleLabel.y,
      fontSize: LAYOUT.header.moduleLabel.fontSize,
      lineHeight: LAYOUT.header.moduleLabel.lineHeight,
      fontWeight: LAYOUT.header.moduleLabel.fontWeight,
      letterSpacing: 0.18,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "mainTitle.text": {
      type: "text",
      x: LAYOUT.header.titleBlock.x + LAYOUT.header.title.x,
      y: LAYOUT.header.titleBlock.y + LAYOUT.header.title.y,
      fontSize: LAYOUT.header.title.fontSize,
      lineHeight: LAYOUT.header.title.lineHeight,
      fontWeight: LAYOUT.header.title.fontWeight,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "subtitle.text": {
      type: "text",
      x: LAYOUT.header.titleBlock.x + LAYOUT.header.subtitle.x,
      y: LAYOUT.header.titleBlock.y + LAYOUT.header.subtitle.y,
      fontSize: LAYOUT.header.subtitle.fontSize,
      lineHeight: LAYOUT.header.subtitle.lineHeight,
      fontWeight: LAYOUT.header.subtitle.fontWeight,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "modulesButton.box": { type: "box", ...LAYOUT.header.modulesButton, scaleX: 1, scaleY: 1 },
    "modulesButton.icon": { type: "svg", ...LAYOUT.header.modulesIcon, scaleX: 1, scaleY: 1, rotate: 0 },
    "modulesButton.text": {
      type: "text",
      ...LAYOUT.header.modulesText,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "progressCard.box": { type: "box", ...LAYOUT.progress.card, scaleX: 1, scaleY: 1 },
    "progress.leftNumber": {
      type: "text",
      ...LAYOUT.progress.leftNumber,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "progress.leftRest": {
      type: "text",
      ...LAYOUT.progress.leftRest,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "progress.rightNumber": {
      type: "text",
      ...LAYOUT.progress.rightNumber,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "progress.rightRest": {
      type: "text",
      ...LAYOUT.progress.rightRest,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    ...Object.fromEntries([...progressSegments, extraProgressSegment]),
    ...lessonCalibration,
    "code.line.01": { type: "text", x: 30, y: 525, fontSize: 20, lineHeight: 34.8, fontWeight: 400, letterSpacing: 0, scaleX: 1, scaleY: 1, fontFamily: "var(--mono)" },
    "code.line.02": { type: "text", x: 30, y: 559.8, fontSize: 20, lineHeight: 34.8, fontWeight: 400, letterSpacing: 0, scaleX: 1, scaleY: 1, fontFamily: "var(--mono)" },
    "code.line.03": { type: "text", x: 30, y: 594.6, fontSize: 20, lineHeight: 34.8, fontWeight: 400, letterSpacing: 0, scaleX: 1, scaleY: 1, fontFamily: "var(--mono)" },
    "code.line.04": { type: "text", x: 30, y: 629.4, fontSize: 20, lineHeight: 34.8, fontWeight: 400, letterSpacing: 0, scaleX: 1, scaleY: 1, fontFamily: "var(--mono)" },
    "code.line.05": { type: "text", x: 30, y: 664.2, fontSize: 20, lineHeight: 34.8, fontWeight: 400, letterSpacing: 0, scaleX: 1, scaleY: 1, fontFamily: "var(--mono)" },
    "bottomNav.box": { type: "box", ...LAYOUT.bottomNav.card, scaleX: 1, scaleY: 1 },
    "bottomNav.menuIcon": { type: "svg", ...LAYOUT.bottomNav.leftIcon, scaleX: 1, scaleY: 1, rotate: 0 },
    "bottomNav.nextText": {
      type: "text",
      ...LAYOUT.bottomNav.bottomNext,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "bottomNav.lessonText": {
      type: "text",
      ...LAYOUT.bottomNav.bottomLesson,
      letterSpacing: 0,
      scaleX: 1,
      scaleY: 1,
      fontFamily: "var(--font)",
    },
    "bottomNav.arrowButton": { type: "box", ...LAYOUT.bottomNav.arrowButton, scaleX: 1, scaleY: 1 },
    "bottomNav.arrowIcon": { type: "svg", ...LAYOUT.bottomNav.arrowIcon, scaleX: 1, scaleY: 1, rotate: 0 },
  } as Record<string, CalibrationElement>,
};

export type CalibrationElementId = string;
export type CalibrationOverrides = Partial<Record<CalibrationElementId, Partial<CalibrationElement>>>;
export const CALIBRATION_ELEMENT_IDS = Object.keys(CALIBRATION_LAYOUT.elements) as CalibrationElementId[];

export function loadPathPoints() {
  try {
    const raw = window.localStorage.getItem("lesson-path-points");
    return raw ? (JSON.parse(raw) as PathPoint[]) : LESSON_PATH_POINTS;
  } catch {
    return LESSON_PATH_POINTS;
  }
}

export function savePathPoints(points: PathPoint[]) {
  window.localStorage.setItem("lesson-path-points", JSON.stringify(points));
}

export function useStageScale(columns = 1) {
  const [scale, setScale] = useState(1);

  useEffect(() => {
    const update = () => {
      const marginX = 28;
      const marginY = 22;
      const gap = columns > 1 ? 28 : 0;
      const next = Math.min((window.innerWidth - marginX - gap) / (STAGE.width * columns), (window.innerHeight - marginY) / STAGE.height);
      setScale(Math.max(0.18, Math.min(1, next)));
    };

    update();
    window.addEventListener("resize", update);
    return () => window.removeEventListener("resize", update);
  }, [columns]);

  return scale;
}

export function stagePointFromEvent(event: MouseEvent<HTMLElement | SVGElement>) {
  const stage = document.querySelector(".stage");
  const rect = stage?.getBoundingClientRect() ?? event.currentTarget.getBoundingClientRect();
  const scaleX = STAGE.width / rect.width;
  const scaleY = STAGE.height / rect.height;

  return {
    x: Math.round((event.clientX - rect.left) * scaleX),
    y: Math.round((event.clientY - rect.top) * scaleY),
  };
}
