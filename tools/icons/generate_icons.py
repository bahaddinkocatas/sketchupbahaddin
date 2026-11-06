# Generates src/Sketch.App/Resources/Icons.xaml: Sketch's own icon set (24x24 grid).
import math, os

K = "#2E3238"    # outline
R = "#DD3B2A"    # action red
B = "#2F7FD8"    # blue
LB = "#CFE2F8"   # light blue fill
G = "#E4E7EB"    # light grey fill
MG = "#B9BFC7"   # mid grey
Y = "#F3B93A"    # tape yellow
GR = "#2E9E48"   # green
W = "#FFFFFF"
O = "#E8862A"    # section orange
PK = "#F2A7A0"   # eraser pink

icons = {}


def L(geom, fill=None, stroke=K, w=1.3, dash=False, cap="Round"):
    return (geom, fill, stroke, w, dash, cap)


def dot(x, y, r=1.7, c=R):
    return L(f"M{x - r},{y} A{r},{r} 0 1 1 {x + r},{y} A{r},{r} 0 1 1 {x - r},{y} Z", c, None)


def circle(x, y, r):
    return f"M{x - r},{y} A{r},{r} 0 1 1 {x + r},{y} A{r},{r} 0 1 1 {x - r},{y} Z"


def arrowhead(x, y, ang, size=3.6, c=R):
    # Filled triangle pointing along angle (degrees, 0 = right, 90 = down).
    a = math.radians(ang)
    tip = (x, y)
    b1 = (x - size * math.cos(a) + size * 0.6 * math.sin(a), y - size * math.sin(a) - size * 0.6 * math.cos(a))
    b2 = (x - size * math.cos(a) - size * 0.6 * math.sin(a), y - size * math.sin(a) + size * 0.6 * math.cos(a))
    f = lambda p: f"{p[0]:.2f},{p[1]:.2f}"
    return L(f"M{f(tip)} L{f(b1)} L{f(b2)} Z", c, None)


# Isometric cube helpers (centre 12,13).
TOP = "M12,3.5 L20.5,8 L12,12.5 L3.5,8 Z"
LEFT = "M3.5,8 L12,12.5 L12,21.5 L3.5,17 Z"
RIGHT = "M12,12.5 L20.5,8 L20.5,17 L12,21.5 Z"
CUBE_OUT = "M12,3.5 L20.5,8 L20.5,17 L12,21.5 L3.5,17 L3.5,8 Z M3.5,8 L12,12.5 L20.5,8 M12,12.5 L12,21.5"


def cube(top=G, left=MG, right="#D2D6DB", w=1.2):
    return [L(TOP, top, None), L(LEFT, left, None), L(RIGHT, right, None), L(CUBE_OUT, None, K, w)]


# ---------------- selection & principal ----------------
ARROW = "M6,2.5 L6,19 L10,15.2 L12.8,21.2 L15.3,20.1 L12.6,14.2 L17.8,14.2 Z"
icons["Select"] = [L(ARROW, W, K, 1.4)]
icons["Lasso"] = [
    L("M3,9 C3,4.5 9,2.5 14,3.2 C19.5,4 22,7.5 20.5,10.5 C19,13.5 13.5,14 9.5,13", None, K, 1.3, dash=True),
    L("M9.5,13 C6.5,14 6,16.5 8,18", None, K, 1.3),
    L("M11,10 L11,22 L13.8,19.2 L15.7,23 L17.3,22.2 L15.5,18.6 L19,18.6 Z", W, K, 1.2),
]
icons["Paint"] = [
    L("M5,9.5 L13.5,4 L19.5,12.5 L11,18 Z", G, K, 1.3),
    L("M5,9.5 C6.5,12 9,15.5 11,18", None, K, 1.3),
    L("M13.5,4 C11.5,2.5 9.5,3.5 9.8,6", None, K, 1.2),
    L("M5,9.5 C3.2,12 2.6,15 3.6,17.6 C4.5,19.8 6.8,19.2 6.6,16.8 C6.4,14.5 5.2,12.5 5,9.5 Z", B, None),
    L("M8.5,13.5 L15,9.2", None, B, 2.2),
]
icons["Eraser"] = [
    L("M3,15.5 L12.5,6 L20.5,14 L14,20.5 L8,20.5 Z", G, K, 1.3),
    L("M3,15.5 L8,20.5 L14,20.5 L16.6,17.9 L8.1,10.4 Z", PK, K, 1.3),
    L("M10,21 L21,21", None, MG, 1.2),
]
icons["MakeComponent"] = [
    L("M7,3 L11.5,5.2 L7,7.4 L2.5,5.2 Z", LB, None), L("M2.5,5.2 L7,7.4 L7,12 L2.5,9.8 Z", B, None), L("M7,7.4 L11.5,5.2 L11.5,9.8 L7,12 Z", "#7FB0E8", None),
    L("M7,3 L11.5,5.2 L11.5,9.8 L7,12 L2.5,9.8 L2.5,5.2 Z", None, K, 1.0),
    L("M17,7 L21.5,9.2 L17,11.4 L12.5,9.2 Z", LB, None), L("M12.5,9.2 L17,11.4 L17,16 L12.5,13.8 Z", B, None), L("M17,11.4 L21.5,9.2 L21.5,13.8 L17,16 Z", "#7FB0E8", None),
    L("M17,7 L21.5,9.2 L21.5,13.8 L17,16 L12.5,13.8 L12.5,9.2 Z", None, K, 1.0),
    L("M9.5,12 L14,14.2 L9.5,16.4 L5,14.2 Z", LB, None), L("M5,14.2 L9.5,16.4 L9.5,21 L5,18.8 Z", B, None), L("M9.5,16.4 L14,14.2 L14,18.8 L9.5,21 Z", "#7FB0E8", None),
    L("M9.5,12 L14,14.2 L14,18.8 L9.5,21 L5,18.8 L5,14.2 Z", None, K, 1.0),
]
icons["TagTool"] = [
    L("M3,5 L12,5 L20.5,13.5 L13.5,20.5 L5,12 L3,10 Z", LB, K, 1.3),
    L(circle(7.5, 9, 1.6), W, K, 1.1),
    L("M11,13 L15,17", None, B, 1.6),
]
icons["Group"] = [
    L("M2.5,2.5 L21.5,2.5 L21.5,21.5 L2.5,21.5 Z", None, K, 1.1, dash=True),
    L("M6,15 L11,7 L16,15 Z", LB, K, 1.2),
    L("M12,11 L18,11 L18,18 L12,18 Z", G, K, 1.2),
]

# ---------------- drawing ----------------
icons["Line"] = [
    L("M3,21 L8,16", None, K, 1.3),
    L("M8,16 L18,6 L21,9 L11,19 Z", Y, K, 1.2),
    L("M18,6 L19.5,4.5 L22.5,7.5 L21,9 Z", R, K, 1.1),
    L("M8,16 L11,19 L7.2,20.2 Z", "#F6DFB8", K, 1.0),
    dot(3, 21, 1.6),
]
icons["Freehand"] = [
    L("M2.5,17 C5,10 8,20 11,13 C13,8.5 15,12 16.5,9", None, K, 1.5),
    L("M16.5,9 L21,4.5 L22.8,6.3 L18.3,10.8 Z", Y, K, 1.0),
    dot(2.5, 17, 1.5),
]
icons["Rectangle"] = [L("M3,6 L21,6 L21,18 L3,18 Z", LB, K, 1.3), dot(3, 18), dot(21, 6)]
icons["RotatedRectangle"] = [L("M3,14 L11,4 L20.5,11.6 L12.5,21.6 Z", LB, K, 1.3), dot(3, 14), dot(11, 4), L("M3,14 L21,14", None, MG, 1.0, dash=True)]
icons["Circle"] = [L(circle(12, 12, 8.8), LB, K, 1.3), L("M12,12 L20.8,12", None, K, 1.0, dash=True), dot(12, 12), dot(20.8, 12, 1.4)]
hexa = " ".join(("M" if i == 0 else "L") + f"{12 + 9 * math.cos(math.radians(60 * i + 30)):.2f},{12 + 9 * math.sin(math.radians(60 * i + 30)):.2f}" for i in range(6)) + " Z"
icons["Polygon"] = [L(hexa, LB, K, 1.3), dot(12, 12)]
icons["ArcCenter"] = [
    L("M21,16 A9,9 0 0 0 3,16", None, K, 1.6),
    L("M12,16 L21,16", None, K, 1.0, dash=True),
    dot(12, 16), dot(21, 16, 1.4), dot(3, 16, 1.4),
]
icons["Arc"] = [
    L("M3,17 A9.5,9.5 0 0 1 21,17", None, K, 1.6),
    L("M3,17 L21,17", None, K, 1.0, dash=True),
    dot(3, 17), dot(21, 17),
]
icons["Arc3Point"] = [
    L("M3,18 C4,9 12,4 21,8", None, K, 1.6),
    dot(3, 18), dot(9.2, 8.6), dot(21, 8),
]
icons["Pie"] = [L("M12,14 L21,14 A9,9 0 0 0 5.6,7.6 Z", LB, K, 1.3), dot(12, 14)]

# ---------------- edit ----------------
icons["Move"] = [
    L("M12,3 L12,21 M3,12 L21,12", None, R, 1.7),
    arrowhead(12, 1.8, -90), arrowhead(12, 22.2, 90), arrowhead(1.8, 12, 180), arrowhead(22.2, 12, 0),
]
icons["PushPull"] = [
    L("M3,15 L12,19.5 L21,15 L12,10.5 Z", LB, K, 1.2),
    L("M3,15 L3,18 L12,22.5 L21,18 L21,15", None, K, 1.2),
    L("M12,22.5 L12,19.5", None, K, 1.2),
    L("M12,15 L12,5", None, R, 1.9),
    arrowhead(12, 2, -90, 4.2),
]
icons["Rotate"] = [
    L("M20,12 A8,8 0 1 1 15.5,4.8", None, R, 1.8),
    arrowhead(19.4, 7.4, 60, 4.0),
    L(circle(12, 12, 2.2), LB, K, 1.1),
]
icons["FollowMe"] = [
    L("M4,20 L4,10 C4,5 8,4 12,4 L20,4", None, R, 1.6),
    arrowhead(22.2, 4, 0, 3.6),
    L("M2,16 L8,16 L8,21 L2,21 Z", LB, K, 1.2),
]
icons["Scale"] = [
    L("M3,8 L16,8 L16,21 L3,21 Z", LB, K, 1.2),
    L("M3,3 L21,3 L21,21", None, K, 1.0, dash=True),
    L("M10,14.5 L19,5.5", None, R, 1.7),
    arrowhead(20.6, 3.9, -45, 3.8),
    L("M14.6,6.6 L17.4,6.6 L17.4,9.4 L14.6,9.4 Z", W, K, 0.9),
]
icons["Offset"] = [
    L("M3,4 L21,4 L21,20 L3,20 Z", LB, K, 1.3),
    L("M7.5,8.5 L16.5,8.5 L16.5,15.5 L7.5,15.5 Z", W, R, 1.6),
]
icons["Axes"] = [
    L("M8,16 L21,16", None, R, 1.6), arrowhead(22.6, 16, 0, 3.4),
    L("M8,16 L17,7.5", None, GR, 1.6), arrowhead(18.3, 6.2, -43, 3.4, GR),
    L("M8,16 L8,3.5", None, B, 1.6), arrowhead(8, 1.8, -90, 3.4, B),
    L("M8,16 L3,21", None, K, 1.0, dash=True),
    dot(8, 16, 1.6, K),
]
icons["Flip"] = [
    L("M12,2 L12,22", None, K, 1.1, dash=True),
    L("M10,5 L10,19 L2.5,19 Z", LB, K, 1.3),
    L("M14,5 L14,19 L21.5,19 Z", B, K, 1.3),
    L("M7.5,3 C9.5,1.2 14.5,1.2 16.5,3", None, R, 1.4), arrowhead(17.3, 4, 45, 3.0),
]

# ---------------- construction ----------------
icons["Tape"] = [
    L(circle(9, 12, 7), Y, K, 1.3),
    L(circle(9, 12, 2.4), W, K, 1.0),
    L("M9,19 L22,19 L22,16 L13,16", "#F8E3A8", K, 1.1),
    L("M16,19 L16,17.3 M19,19 L19,17.3", None, K, 0.9),
    L("M22,16 L22,21", None, R, 1.5),
]
icons["Dimension"] = [
    L("M3,5 L3,20 M21,5 L21,20", None, K, 1.1),
    L("M4.5,12 L19.5,12", None, K, 1.3),
    arrowhead(3.3, 12, 180, 3.4, K), arrowhead(20.7, 12, 0, 3.4, K),
    L("M8,9.5 L16,9.5", None, R, 1.4),
]
icons["Protractor"] = [
    L("M2.5,18 A9.5,9.5 0 0 1 21.5,18 Z", LB, K, 1.3),
    L("M12,18 L12,15.5 M5.3,11.3 L7.1,13 M18.7,11.3 L16.9,13 M12,8.5 L12,10.5", None, K, 1.0),
    L("M12,18 L19,6", None, R, 1.6), dot(12, 18, 1.6),
]
icons["Text"] = [
    L("M8,3 L21,3 L21,13 L13,13 L9,16.5 L9.6,13 L8,13 Z", W, K, 1.2),
    L("M3,21 L8.5,14", None, K, 1.1), dot(3, 21, 1.5),
    L("M12,10.5 L14.4,5 L16.8,10.5 M13,8.7 L15.8,8.7", None, B, 1.2),
]
icons["SectionPlane"] = [
    L("M3,7 L15,3 L21,17 L9,21 Z", "#FBE2C7", O, 1.6),
    L("M10,12 L5,13.6", None, R, 1.5), arrowhead(3.4, 14.1, 162, 3.2),
    L("M14,11 L19,9.4", None, R, 1.5), arrowhead(20.6, 8.9, -18, 3.2),
]
icons["Text3D"] = [
    L("M5,19 L10,4 L14,4 L19,19 L15.5,19 L14.4,15.5 L9.6,15.5 L8.5,19 Z M10.6,12.5 L13.4,12.5 L12,8 Z", LB, K, 1.2),
    L("M14,4 L16.5,2.5 L21.5,17.5 L19,19 M19,19 L21.5,17.5", None, K, 1.0),
    L("M8.5,19 L11,17.5 L15.5,19", None, MG, 0.9),
]

# ---------------- camera ----------------
icons["Orbit"] = [
    L("M2,12 A10,4.2 0 0 1 22,12", None, MG, 1.3),
    L(circle(12, 12, 6), LB, K, 1.2),
    L("M8.5,9.5 A3.5,3.5 0 0 1 12,7", None, W, 1.4),
    L("M22,12 A10,4.2 0 0 1 4.5,14.8", None, R, 1.7),
    arrowhead(2.6, 13.6, 200, 3.8),
]
icons["Pan"] = [
    L("M7.5,12 L7.5,6 C7.5,4.6 9.6,4.6 9.6,6 L9.6,11 L9.6,4.2 C9.6,2.8 11.7,2.8 11.7,4.2 L11.7,11 L11.7,5 C11.7,3.6 13.8,3.6 13.8,5 "
      "L13.8,11.5 L13.8,7 C13.8,5.6 15.9,5.6 15.9,7 L15.9,15 C15.9,19 13.5,21.5 10.5,21.5 C8,21.5 6.8,20 5.6,18 L3.4,13.8 "
      "C2.8,12.6 4.4,11.6 5.3,12.6 L7.5,15 Z", "#FBE6D2", K, 1.1),
]
icons["Zoom"] = [
    L(circle(10, 10, 6.8), LB, K, 1.5),
    L("M15,15 L21.3,21.3", None, K, 3.0),
    L("M7,10 L13,10 M10,7 L10,13", None, R, 1.6),
]
icons["ZoomWindow"] = [
    L("M2.5,2.5 L14.5,2.5 L14.5,12 M2.5,2.5 L2.5,12 L7,12", None, K, 1.0, dash=True),
    L(circle(13.5, 13.5, 5), LB, K, 1.4),
    L("M17.2,17.2 L21.5,21.5", None, K, 2.6),
    L("M11.3,13.5 L15.7,13.5 M13.5,11.3 L13.5,15.7", None, R, 1.3),
]
icons["ZoomExtents"] = [
    L("M2,7 L2,2 L7,2 M17,2 L22,2 L22,7 M22,17 L22,22 L17,22 M7,22 L2,22 L2,17", None, R, 1.6),
    L(circle(11, 11, 4.6), LB, K, 1.4),
    L("M14.4,14.4 L18.5,18.5", None, K, 2.6),
]
icons["Previous"] = [
    L("M3,8 L16,8 L16,19 L3,19 Z", G, K, 1.2),
    L("M16,11 L21,8.5 L21,18.5 L16,16 Z", MG, K, 1.1),
    L(circle(9.5, 13.5, 2.6), LB, K, 1.0),
    L("M13,5 L5,5", None, R, 1.6), arrowhead(3.2, 5, 180, 3.4),
]
icons["Search"] = [
    L(circle(10, 10, 6.8), W, K, 1.5),
    L("M15,15 L21.3,21.3", None, K, 3.0),
    L("M6.8,8 L13.2,8 M6.8,10.5 L13.2,10.5 M6.8,13 L11,13", None, B, 1.2),
]

# ---------------- extensions ----------------
# DEAP evidence: a dwelling outline with an area table and a north arrow.
icons["Deap"] = [
    L("M2.5,11 L10,4.5 L17.5,11 L17.5,21.5 L2.5,21.5 Z", LB, K, 1.3),
    L("M5.5,13 L14.5,13 L14.5,19.5 L5.5,19.5 Z", W, K, 1.0),
    L("M5.5,15.2 L14.5,15.2 M5.5,17.4 L14.5,17.4 M9,13 L9,19.5", None, B, 1.0),
    L("M20.5,9.5 L20.5,2.5", None, R, 1.6), arrowhead(20.5, 1.2, -90, 3.4),
    L("M19,11.5 L19,14.5 L22,11.5 L22,14.5", None, R, 1.1),
]

# ---------------- views (cube with the viewed face in blue) ----------------
icons["ViewIso"] = cube(LB, B, "#7FB0E8")
icons["ViewTop"] = cube(B, G, "#D2D6DB")
icons["ViewFront"] = cube(G, B, "#D2D6DB")
icons["ViewRight"] = cube(G, MG, B)
icons["ViewLeft"] = cube(G, MG, "#D2D6DB") + [L("M3.5,8 L3.5,17", None, B, 2.6)]
icons["ViewBack"] = cube(G, MG, "#D2D6DB") + [L("M12,3.5 L20.5,8 L20.5,17", None, B, 2.0, dash=True)]
icons["ViewBottom"] = cube(G, MG, "#D2D6DB") + [L("M3.5,17 L12,21.5 L20.5,17", None, B, 2.6)]

# ---------------- standard ----------------
icons["Undo"] = [L("M8,7 L15,7 C19,7 21,9.5 21,13 C21,16.5 18.5,19 15,19 L7,19", None, B, 1.8), arrowhead(3.5, 7, 180, 4.2, B)]
icons["Redo"] = [L("M16,7 L9,7 C5,7 3,9.5 3,13 C3,16.5 5.5,19 9,19 L17,19", None, B, 1.8), arrowhead(20.5, 7, 0, 4.2, B)]
icons["New"] = [L("M5,2.5 L14,2.5 L19,7.5 L19,21.5 L5,21.5 Z", W, K, 1.3), L("M14,2.5 L14,7.5 L19,7.5", G, K, 1.1), L("M12,11 L12,18 M8.5,14.5 L15.5,14.5", None, R, 1.5)]
icons["Open"] = [L("M2.5,5 L9,5 L11,7.5 L19,7.5 L19,10 L2.5,10 Z", "#F6D38A", K, 1.2), L("M2.5,19.5 L5.5,10 L22,10 L19,19.5 Z", Y, K, 1.2)]
icons["Save"] = [L("M3,3 L18,3 L21,6 L21,21 L3,21 Z", B, K, 1.2), L("M7,3 L7,9 L16,9 L16,3", W, K, 1.0), L("M6,13 L18,13 L18,21 L6,21 Z", W, K, 1.0), L("M13,4.5 L14.5,4.5 L14.5,7.5 L13,7.5 Z", K, None)]
icons["Image"] = [L("M2.5,4.5 L21.5,4.5 L21.5,19.5 L2.5,19.5 Z", W, K, 1.3), L("M4.5,17.5 L9.5,10.5 L13,15 L15.5,12.5 L19.5,17.5 Z", "#8CC27A", None), L(circle(16.5, 8.5, 1.8), Y, None)]

# ---------------- styles ----------------
icons["XRay"] = [L(TOP, "#9CC4F0", None), L(LEFT, "#9CC4F0", None), L(RIGHT, "#BBD6F5", None),
                 L("M3.5,17 L12,12.5 L20.5,17 M12,12.5 L12,3.5", None, K, 0.9, dash=True), L(CUBE_OUT, None, K, 1.2)]
icons["BackEdges"] = cube(W, W, W) + [L("M3.5,17 L12,12.8 L20.5,17", None, K, 0.9, dash=True)]
icons["ColorByTag"] = [L("M3,4 L10,4 L10,11 L3,11 Z", "#E36B5D", K, 1.0), L("M14,4 L21,4 L21,11 L14,11 Z", "#5DA9E3", K, 1.0),
                       L("M3,14 L10,14 L10,21 L3,21 Z", "#6CC27A", K, 1.0), L("M14,14 L21,14 L21,21 L14,21 Z", "#F3C14B", K, 1.0)]
icons["Style.Wireframe"] = [L(CUBE_OUT, None, K, 1.2)]
icons["Style.HiddenLine"] = cube(W, W, W)
icons["Style.Shaded"] = cube("#E6DCC8", "#B9AE98", "#D3C8B2")
icons["Style.ShadedWithTextures"] = cube("#E8C27E", "#9C6B3E", "#C08A52") + [L("M6,12 L10,14 M6,15 L10,17 M14,15 L18.5,12.6", None, "#7A4F2B", 0.9)]
icons["Style.Monochrome"] = cube("#F2F2F2", "#9A9A9A", "#D0D0D0")


def layer_xaml(layer):
    geom, fill, stroke, w, dash, cap = layer
    brush = f' Brush="{fill}"' if fill else ""
    if stroke is None:
        return f'                <GeometryDrawing{brush} Geometry="{geom}" />'
    dash_xml = '<Pen.DashStyle><DashStyle Dashes="2,1.6" /></Pen.DashStyle>' if dash else ""
    pen = (f'<Pen Brush="{stroke}" Thickness="{w}" StartLineCap="{cap}" EndLineCap="{cap}" LineJoin="Round"'
           + (f'>{dash_xml}</Pen>' if dash else ' />'))
    return (f'                <GeometryDrawing{brush} Geometry="{geom}">\n'
            f'                    <GeometryDrawing.Pen>{pen}</GeometryDrawing.Pen>\n'
            f'                </GeometryDrawing>')


out = ['<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
       '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
       '    <!-- Sketch\'s own icon set (24x24 design grid). Generated; edit the generator rather than this file. -->']
for name, layers in icons.items():
    out.append(f'    <DrawingImage x:Key="Icon.{name}">')
    out.append('        <DrawingImage.Drawing>')
    # A transparent 24x24 frame keeps every icon the same size and position.
    out.append('            <DrawingGroup>')
    out.append('                <GeometryDrawing Brush="#00FFFFFF" Geometry="M0,0 L24,0 L24,24 L0,24 Z" />')
    out.extend(layer_xaml(l) for l in layers)
    out.append('            </DrawingGroup>')
    out.append('        </DrawingImage.Drawing>')
    out.append('    </DrawingImage>')
    out.append('')
out.append('</ResourceDictionary>')
target = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'src', 'Sketch.App', 'Resources', 'Icons.xaml')
open(target, 'w', encoding='utf-8', newline='\n').write("\n".join(out) + "\n")
print(len(icons), 'icons')
