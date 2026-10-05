#!/usr/bin/env python3
"""Parse corrected Wilaya2 Excel (yellow keep/add, red modify)."""
from __future__ import annotations

import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

XLSX = Path(r"C:\Users\Marvel\Desktop\Wilaya2_Payers_2026-10-01.xlsx")
OUT = Path(__file__).with_name("_wilaya2_correct_utf8.txt")
NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}


def main() -> None:
    with zipfile.ZipFile(XLSX) as z:
        ss = ET.fromstring(z.read("xl/sharedStrings.xml"))
        strings: list[str] = []
        for si in ss.findall("m:si", NS):
            texts = [t.text or "" for t in si.findall(".//m:t", NS)]
            strings.append("".join(texts))

        styles = ET.fromstring(z.read("xl/styles.xml"))
        fills = styles.findall("m:fills/m:fill", NS)
        fill_colors: list[str | None] = []
        for f in fills:
            pf = f.find("m:patternFill", NS)
            if pf is None:
                fill_colors.append(None)
                continue
            fg = pf.find("m:fgColor", NS)
            if fg is None:
                fill_colors.append(None)
                continue
            fill_colors.append(
                fg.attrib.get("rgb")
                or fg.attrib.get("indexed")
                or fg.attrib.get("theme")
            )
        cell_xfs = styles.findall("m:cellXfs/m:xf", NS)

        def style_fill(si: str | None) -> str | None:
            if si is None:
                return None
            xf = cell_xfs[int(si)]
            fi = xf.attrib.get("fillId")
            if fi is None:
                return None
            return fill_colors[int(fi)]

        sheet = ET.fromstring(z.read("xl/worksheets/sheet1.xml"))
        lines = ["row\tfill\tseq\tpayId\tcustId\tname\tamount\tdate\tlist"]
        for row in sheet.findall("m:sheetData/m:row", NS):
            r = int(row.attrib["r"])
            cells: dict[str, str] = {}
            fill_a = None
            for c in row.findall("m:c", NS):
                ref = c.attrib["r"]
                col = "".join(ch for ch in ref if ch.isalpha())
                t = c.attrib.get("t")
                v = c.find("m:v", NS)
                val = ""
                if v is not None and v.text is not None:
                    val = strings[int(v.text)] if t == "s" else v.text
                cells[col] = val
                if col == "A":
                    fill_a = style_fill(c.attrib.get("s"))
                elif fill_a is None and col == "D":
                    fill_a = style_fill(c.attrib.get("s"))
            lines.append(
                "\t".join(
                    [
                        str(r),
                        str(fill_a),
                        cells.get("A", ""),
                        cells.get("B", ""),
                        cells.get("C", ""),
                        cells.get("D", ""),
                        cells.get("E", ""),
                        cells.get("F", ""),
                        cells.get("G", ""),
                    ]
                )
            )
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(OUT.read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
