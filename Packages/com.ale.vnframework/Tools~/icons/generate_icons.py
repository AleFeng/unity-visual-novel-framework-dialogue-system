#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
生成 VN 演出播放控制按钮条用的图标。

风格：白色简约矢量，透明底；每个功能出「关 / 开」两态，开态带微微外发光。

做法：在 4 倍尺寸（512×512）的单通道蒙版上画形状，再 LANCZOS 下采样到 128×128——
这样多边形与文字边缘都能拿到干净的抗锯齿，而不必自己写覆盖率计算。
外发光是把形状蒙版高斯模糊后提亮，用 lighter 合成到形状**下方**，
所以形状本身始终是实心纯白、不会被发光糊掉。

「1x / 2x / 3x」与「NEW」用 LiberationSans 渲染。该字体随 TextMesh Pro 附带并带 OFL 许可证；
OFL 约束的是字体软件本身的再分发，不限制字形的光栅化产物，故渲染结果可以作为 PNG 提交。

用法（仓库根目录下）：
    python "Packages/com.ale.vnframework/Tools~/icons/generate_icons.py"

输出到样例的 Assets/UI/Icons/，覆盖同名文件。

⚠️ 输出路径指向的是 **Assets/Samples/… 这份镜像**（能在 Unity 里立刻看到效果），
而随包发布的源在 Packages/com.ale.vnframework/Samples~/VN Framework Demo/Assets/UI/Icons/。
改完图标记得把两边同步回去，规则与样例里其它文件一致：

    diff -rq "Assets/Samples/Ale VN Framework/1.0.0/VN Framework Demo" \
             "Packages/com.ale.vnframework/Samples~/VN Framework Demo"

新生成的文件在 Unity 里默认是 Default 贴图，需要把 Texture Type 设为 Sprite
（已有 .meta 的同名覆盖不受影响，保持原设置）。
"""

import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

# ---------------------------------------------------------------- 基本参数

SIZE = 128                      # 最终输出尺寸
SS = 4                          # 超采样倍数
R = SIZE * SS                   # 绘制尺寸 = 512
C = R // 2                      # 中心

OFF_ALPHA = 0.70                # 关态整体不透明度。纯白对纯白只差一层微光的话两态几乎分不出来，
                                # 故关态压暗一档——仍是白色，只是弱一些。
GLOW_BLUR = 16                  # 发光模糊半径（512 尺度）
GLOW_GAIN = 1.45                # 模糊后提亮系数：近场接近饱和、远场快速衰减，读起来才像「发光」

# 仓库根 = 本文件往上四层（icons -> Tools~ -> com.ale.vnframework -> Packages -> 根）
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
OUT_DIR = os.path.join(REPO, "Assets", "Samples", "Ale VN Framework", "1.0.0",
                       "VN Framework Demo", "Assets", "UI", "Icons")
FONT_PATH = os.path.join(REPO, "Assets", "TextMesh Pro", "Fonts", "LiberationSans.ttf")


# ---------------------------------------------------------------- 通用工具

def new_mask():
    """新建一张 512×512 的单通道蒙版（0 = 空，255 = 实心）。"""
    return Image.new("L", (R, R), 0)


def save(mask, name, glow):
    """把蒙版落成 PNG。glow=True 时先合成外发光，再下采样并与纯白 RGB 合并。"""
    if glow:
        halo = mask.filter(ImageFilter.GaussianBlur(GLOW_BLUR))
        halo = halo.point(lambda v: min(255, int(v * GLOW_GAIN)))
        alpha = ImageChops.lighter(halo, mask)
    else:
        alpha = mask.point(lambda v: int(v * OFF_ALPHA))

    alpha = alpha.resize((SIZE, SIZE), Image.LANCZOS)
    white = Image.new("L", (SIZE, SIZE), 255)
    img = Image.merge("RGBA", (white, white, white, alpha))

    path = os.path.join(OUT_DIR, name + ".png")
    img.save(path, "PNG")
    return path


def font(px):
    return ImageFont.truetype(FONT_PATH, px)


def draw_centered_text(d, text, px, stroke=0, dy=0):
    """把文字画在画布正中。用 textbbox 量真实墨迹范围，而不是 font 的行高——
    否则 '1x' 这种没有下伸部的字符串会明显偏上。"""
    f = font(px)
    box = d.textbbox((0, 0), text, font=f, stroke_width=stroke)
    w = box[2] - box[0]
    h = box[3] - box[1]
    x = C - w / 2 - box[0]
    y = C - h / 2 - box[1] + dy
    d.text((x, y), text, font=f, fill=255, stroke_width=stroke, stroke_fill=255)


# ---------------------------------------------------------------- 各个图形

def shape_play():
    """自动播放：右向实心三角。顶点按重心落在画布中心来配。"""
    m = new_mask()
    ImageDraw.Draw(m).polygon([(183, 106), (183, 406), (403, 256)], fill=255)
    return m


def shape_forward():
    """快进：两个右向三角并排（⏩）。中间留一道缝，不然会糊成一个梯形。"""
    m = new_mask()
    d = ImageDraw.Draw(m)
    d.polygon([(130, 116), (130, 396), (248, 256)], fill=255)
    d.polygon([(262, 116), (262, 396), (380, 256)], fill=255)
    return m


def shape_eye():
    """隐藏UI：简约眼睛。

    眼眶是两段圆弧围成的橄榄形（vesica）。直接画两段 arc 而不是「实心形状挖内部」——
    挖内部需要形状的等距内缩，PIL 没有腐蚀操作，非等比缩放又会让上下描边变细；
    而两段 arc 用 width= 画出来的描边天生是等宽的。
    """
    m = new_mask()
    d = ImageDraw.Draw(m)

    # 半宽 = sqrt(r2^2 - r1^2)，半高 = r2 - r1。取 170 × 85——半高再小的话
    # 眼睛的视觉重量会明显轻于同排其它图标，一排摆开就显得它「瘦了一圈」。
    r1 = 127.5
    r2 = 212.5
    stroke = 20

    # 上边缘：圆心在下方，取经过正上方的那一段
    d.arc([C - r2, C + r1 - r2, C + r2, C + r1 + r2], 216.87, 323.13, fill=255, width=stroke)
    # 下边缘：圆心在上方，取经过正下方的那一段
    d.arc([C - r2, C - r1 - r2, C + r2, C - r1 + r2], 36.87, 143.13, fill=255, width=stroke)

    d.ellipse([C - 42, C - 42, C + 42, C + 42], fill=255)   # 瞳孔
    return m


def shape_new():
    """新对话停止：圆角框里的 NEW。"""
    m = new_mask()
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([76, 150, 436, 362], radius=44, outline=255, width=20)
    draw_centered_text(d, "NEW", 118, stroke=4)
    return m


def shape_speed(n):
    """播放速度：1x / 2x / 3x。stroke_width 给 Regular 字重补一点厚度，免得缩到 128 太细。"""
    m = new_mask()
    draw_centered_text(ImageDraw.Draw(m), "%dx" % n, 236, stroke=7)
    return m


# ---------------------------------------------------------------- 主流程

ICONS = [
    ("T_VnCtrl_Auto", shape_play),
    ("T_VnCtrl_Forward", shape_forward),
    ("T_VnCtrl_Hide", shape_eye),
    ("T_VnCtrl_New", shape_new),
    ("T_VnCtrl_Speed1", lambda: shape_speed(1)),
    ("T_VnCtrl_Speed2", lambda: shape_speed(2)),
    ("T_VnCtrl_Speed3", lambda: shape_speed(3)),
]


def main():
    if not os.path.isfile(FONT_PATH):
        sys.exit("找不到字体：%s" % FONT_PATH)
    os.makedirs(OUT_DIR, exist_ok=True)

    written = []
    for name, build in ICONS:
        mask = build()
        written.append(save(mask, name + "_Off", glow=False))
        written.append(save(mask, name + "_On", glow=True))

    for p in written:
        print("%7d  %s" % (os.path.getsize(p), os.path.relpath(p, REPO).replace("\\", "/")))
    print("共 %d 张 -> %s" % (len(written), os.path.relpath(OUT_DIR, REPO).replace("\\", "/")))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
