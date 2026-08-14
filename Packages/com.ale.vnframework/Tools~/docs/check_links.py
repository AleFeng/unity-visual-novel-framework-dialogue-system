#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
检查仓库内 Markdown 的本地链接与锚点。

用法（仓库根目录下）：
    python "Packages/com.ale.vnframework/Tools~/docs/check_links.py"

退出码：0 = 全通过，1 = 有断链或锚点失效。

--------------------------------------------------------------------------
为什么这个脚本要留在仓库里
--------------------------------------------------------------------------
锚点 slug 的生成规则很容易写错，而且**错的方式每次都不一样**，前后已经踩过三轮：

  · 1.2.0：emoji 开头的标题（`## 📜 目录` → `#-目录`）算错了两处；
  · 1.4.0：链接里的 `%20`（`Third%20Party%20Notices.md`）没有 URL 解码，误报断链；
  · 1.5.0：用「码位 > 0x2FFF 就保留」来区分中文，结果把 emoji（类别 So）和
           中文引号「」（类别 Ps / Pe）也一起保留了，一次误报 34 条。

根因都是**用码位范围去猜字符类别**。正确做法是查 Unicode 类别：
GitHub 的 slug 只保留字母（L*）、数字（N*）、连字符与下划线，空格转连字符，
其余（标点 P*、符号 S*，含 emoji）全部丢弃——唯一的例外是变体选择符 U+FE0F 会被保留，
所以 `## 🖥️ 欢迎窗口` 的锚点是 `#️-欢迎窗口`（开头那个「空字符」就是它）。
"""

import os
import re
import sys
import unicodedata
from urllib.parse import unquote

SKIP_DIRS = {".git", "Library", "Temp", "obj", "Logs", "node_modules", "Live2D", "PluginsIgnore"}

# 变体选择符：GitHub 不会把它从 slug 里去掉，emoji 本体却会被去掉。
VARIATION_SELECTOR = "️"


def slugify(text):
    """把标题文本转成 GitHub 风格的锚点 slug。"""
    text = re.sub(r"`([^`]*)`", r"\1", text)              # 行内代码取内容
    text = re.sub(r"!?\[([^\]]*)\]\([^)]*\)", r"\1", text)  # 链接 / 图片取文本
    text = re.sub(r"<[^>]+>", "", text)                    # 内嵌 HTML 标签
    text = re.sub(r"[*_~]", "", text)                      # 强调标记

    out = []
    for ch in text.strip().lower():
        if ch == VARIATION_SELECTOR:
            out.append(ch)
            continue
        cat = unicodedata.category(ch)
        if cat[0] in ("L", "N") or ch in "-_":
            out.append(ch)
        elif ch.isspace():
            out.append("-")
        # 其余（P* 标点、S* 符号含 emoji、C* 控制符）一律丢弃
    return "".join(out)


def collect_markdown(root="."):
    found = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            if name.endswith(".md"):
                found.append(os.path.normpath(os.path.join(dirpath, name)))
    return found


def headings_of(path, cache):
    """取某份文档的全部锚点。围栏代码块里的 # 不算标题。"""
    if path in cache:
        return cache[path]
    result = set()
    try:
        with open(path, encoding="utf-8") as fp:
            in_fence = False
            for line in fp:
                if line.strip().startswith("```"):
                    in_fence = not in_fence
                    continue
                if in_fence:
                    continue
                m = re.match(r"^#{1,6}\s+(.*)$", line)
                if m:
                    result.add(slugify(m.group(1)))
    except OSError:
        pass
    cache[path] = result
    return result


def main():
    sys.stdout.reconfigure(encoding="utf-8")

    files = collect_markdown()
    cache = {}
    broken_file = broken_anchor = checked = 0

    for path in files:
        text = open(path, encoding="utf-8").read()
        for m in re.finditer(r"\[([^\]]*)\]\(([^)\s]+)\)", text):
            href = m.group(2)
            if href.startswith(("http://", "https://", "mailto:", "#!")):
                continue
            checked += 1

            filepart, _, anchor = href.partition("#")
            filepart, anchor = unquote(filepart), unquote(anchor)

            target = path
            if filepart:
                target = os.path.normpath(os.path.join(os.path.dirname(path), filepart))
                if not os.path.exists(target):
                    print(f"[断链]     {path} -> {href}")
                    broken_file += 1
                    continue

            if anchor and target.endswith(".md"):
                if slugify(anchor) not in headings_of(target, cache):
                    print(f"[锚点失效] {path} -> {href}")
                    broken_anchor += 1

    # 围栏配对：奇数条 ``` 说明有代码块没闭合，会把后文整段吞掉
    fence_bad = 0
    for path in files:
        n = sum(1 for line in open(path, encoding="utf-8") if line.strip().startswith("```"))
        if n % 2:
            print(f"[围栏未闭合] {path}: {n} 条 ```")
            fence_bad += 1

    print(f"\n{len(files)} 份文档 / {checked} 条本地链接："
          f"断链 {broken_file}，锚点失效 {broken_anchor}，围栏未闭合 {fence_bad}")
    return 1 if (broken_file or broken_anchor or fence_bad) else 0


if __name__ == "__main__":
    sys.exit(main())
