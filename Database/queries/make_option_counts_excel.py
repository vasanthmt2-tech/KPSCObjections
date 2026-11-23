# Builds "OMR Option Counts by Version.xlsx" from the output of
# OmrOptionCountsByVersion.sql, saved as CSV:
#   sqlcmd -S <server> -E -d KPSC_OBJECTIONS -W -h -1 -s "," -i OmrOptionCountsByVersion.sql -o counts.csv
#   python make_option_counts_excel.py counts.csv "OMR Option Counts by Version.xlsx"
# One block per master question: Option 1..5, Blank and More than one down,
# versions across (A..L for Paper I, M..X for Paper II).
import sys, csv
from collections import defaultdict
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side

src, dst = sys.argv[1], sys.argv[2]

counts = defaultdict(int)
questions = defaultdict(set)
for row in csv.reader(open(src, encoding="utf-8", errors="replace")):
    if len(row) != 5 or row[0].strip() not in ("P1", "P2"):
        continue
    paper, mq, version, option, n = (c.strip() for c in row)
    counts[(paper, int(mq), version, option)] = int(n)
    questions[paper].add(int(mq))

thin = Side(style="thin", color="999999")
border = Border(left=thin, right=thin, top=thin, bottom=thin)
head_fill = PatternFill("solid", fgColor="1F4E79")
label_fill = PatternFill("solid", fgColor="DDEBF7")

wb = openpyxl.Workbook()
wb.remove(wb.active)

for paper, title, first in (("P1", "Paper I (A-L)", "A"), ("P2", "Paper II (M-X)", "M")):
    versions = [chr(ord(first) + i) for i in range(12)]
    ws = wb.create_sheet(title)
    ws.column_dimensions["A"].width = 18
    for i in range(12):
        ws.column_dimensions[openpyxl.utils.get_column_letter(i + 2)].width = 10

    r = 1
    for mq in sorted(questions[paper]):
        cell = ws.cell(r, 1, "Master Qno %d" % mq)
        cell.font = Font(bold=True, color="FFFFFF"); cell.fill = head_fill; cell.border = border
        for i, v in enumerate(versions):
            c = ws.cell(r, i + 2, v)
            c.font = Font(bold=True, color="FFFFFF"); c.fill = head_fill; c.border = border
            c.alignment = Alignment(horizontal="center")
        rows = [("Option %d" % o, str(o)) for o in range(1, 6)] + [("Blank", "Blank"), ("More than one", "Multiple")]
        for n, (text, key) in enumerate(rows, 1):
            label = ws.cell(r + n, 1, text)
            label.fill = label_fill; label.border = border
            for i, v in enumerate(versions):
                c = ws.cell(r + n, i + 2, counts.get((paper, mq, v, key), 0))
                c.border = border; c.number_format = "#,##0"
        r += len(rows) + 2  # header, the rows, one empty row
    ws.freeze_panes = "B1"

wb.save(dst)
print("questions:", {p: len(q) for p, q in questions.items()}, "cells:", len(counts))
