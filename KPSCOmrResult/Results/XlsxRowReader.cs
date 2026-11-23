using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace KPSCOmrResult.Results
{
    /// <summary>
    /// Reads the first worksheet of an .xlsx file one row at a time.
    ///
    /// The OMR result file has 112 columns and up to about 150,000 rows per
    /// paper. Loading such a workbook whole (as ClosedXML does) needs several
    /// gigabytes; this reader keeps only the current row and the workbook's
    /// shared strings in memory.
    /// </summary>
    public sealed class XlsxRowReader : IDisposable
    {
        private readonly SpreadsheetDocument document;
        private readonly OpenXmlReader reader;
        private readonly List<string> sharedStrings = new List<string>();

        public XlsxRowReader(Stream stream)
        {
            document = SpreadsheetDocument.Open(stream, false);

            WorkbookPart workbook = document.WorkbookPart;
            Sheet first = workbook.Workbook.Sheets.Elements<Sheet>().FirstOrDefault();

            if (first == null)
                throw new InvalidDataException("The Excel file has no worksheet.");

            if (workbook.SharedStringTablePart != null)
            {
                using (OpenXmlReader strings = OpenXmlReader.Create(workbook.SharedStringTablePart))
                {
                    while (strings.Read())
                    {
                        if (strings.ElementType == typeof(SharedStringItem) && strings.IsStartElement)
                            sharedStrings.Add(((SharedStringItem)strings.LoadCurrentElement()).InnerText);
                    }
                }
            }

            reader = OpenXmlReader.Create((WorksheetPart)workbook.GetPartById(first.Id));
        }

        /// <summary>
        /// The next row that has any cell: its 1-based row number and its cell
        /// texts by column (index 0 is column A). Missing cells are "".
        /// </summary>
        public bool Read(out int rowNumber, out string[] cells)
        {
            while (reader.Read())
            {
                if (reader.ElementType != typeof(Row) || !reader.IsStartElement)
                    continue;

                Row row = (Row)reader.LoadCurrentElement();
                rowNumber = row.RowIndex != null ? (int)row.RowIndex.Value : 0;

                List<KeyValuePair<int, string>> values = new List<KeyValuePair<int, string>>();
                int next = 0;

                foreach (Cell cell in row.Elements<Cell>())
                {
                    int column = cell.CellReference != null ? ColumnIndex(cell.CellReference.Value) : next;
                    next = column + 1;
                    values.Add(new KeyValuePair<int, string>(column, Text(cell)));
                }

                int width = values.Count == 0 ? 0 : values.Max(v => v.Key) + 1;
                cells = Enumerable.Repeat("", width).ToArray();

                foreach (KeyValuePair<int, string> v in values)
                    cells[v.Key] = v.Value;

                return true;
            }

            rowNumber = 0;
            cells = null;
            return false;
        }

        private string Text(Cell cell)
        {
            if (cell.DataType != null && cell.DataType.Value == CellValues.InlineString)
                return cell.InlineString != null ? cell.InlineString.InnerText : "";

            string raw = cell.CellValue != null ? cell.CellValue.Text : "";

            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                int index;
                return int.TryParse(raw, out index) && index >= 0 && index < sharedStrings.Count
                    ? sharedStrings[index]
                    : "";
            }

            return raw ?? "";
        }

        /// <summary>"A1" is 0, "Z9" is 25, "AA3" is 26.</summary>
        private static int ColumnIndex(string reference)
        {
            int index = 0;

            foreach (char c in reference)
            {
                if (c < 'A' || c > 'Z')
                    break;

                index = index * 26 + (c - 'A' + 1);
            }

            return index - 1;
        }

        public void Dispose()
        {
            reader.Dispose();
            document.Dispose();
        }
    }
}
