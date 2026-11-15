using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace KPSCObjections.Payments
{
    /// <summary>
    /// Prints one line of text on a white strip across the top of every page
    /// of a PDF, so each page of a supporting document can be traced to its
    /// objection after it is printed or separated.
    /// </summary>
    public static class PdfHeaderStamper
    {
        private const double StripHeight = 16;     // points (about 5.6 mm), one line
        private const double LineHeight = 10;      // per line when the text wraps
        private const double LinePadding = 3;
        private const double SideMargin = 8;
        private const double FontSize = 8;
        private const double MinFontSize = 4.5;
        private const string FontName = "Arial";

        /// <summary>
        /// Returns a copy of the PDF with the header on every page. Throws
        /// when the file cannot be opened for editing (for example a
        /// password-protected PDF); the caller should ask for another file.
        /// </summary>
        public static byte[] Stamp(byte[] pdf, string header)
        {
            using (MemoryStream input = new MemoryStream(pdf))
            using (PdfDocument document = PdfReader.Open(input, PdfDocumentOpenMode.Modify))
            {
                if (document.PageCount == 0)
                    throw new InvalidOperationException("The PDF has no pages.");

                foreach (PdfPage page in document.Pages)
                    StampPage(page, header);

                using (MemoryStream output = new MemoryStream())
                {
                    document.Save(output, false);
                    return output.ToArray();
                }
            }
        }

        private static void StampPage(PdfPage page, string header)
        {
            // Sizes of the page as stored (before any /Rotate is applied).
            PdfRectangle media = page.MediaBox;
            double pageWidth = media.Width, pageHeight = media.Height;

            // The visible part of the page, measured from its stored top-left
            // corner. Only read /CropBox when the page has one: asking
            // PDFsharp for a missing CropBox adds an empty one, which makes
            // viewers show the page at the wrong size.
            double cropLeft = 0, cropTop = 0, cropWidth = pageWidth, cropHeight = pageHeight;

            if (page.Elements.ContainsKey("/CropBox"))
            {
                PdfRectangle crop = page.Elements.GetRectangle("/CropBox");

                if (crop != null && crop.Width > 0 && crop.Height > 0)
                {
                    cropLeft = crop.X1 - media.X1;
                    cropTop = media.Y2 - crop.Y2;
                    cropWidth = crop.Width;
                    cropHeight = crop.Height;
                }
            }

            int rotation = ((page.Rotate % 360) + 360) % 360;

            // Append draws over the existing content without disturbing it.
            using (XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
            {
                // PDFsharp measures a rotated page from its displayed height;
                // this brings the origin back to the stored top-left corner.
                gfx.TranslateTransform(0, gfx.PageSize.Height - pageHeight);

                // A viewer turns the page by /Rotate when showing it. Turn the
                // drawing the opposite way, so that from here on (0,0) is the
                // top-left of the page as the reader sees it. left/top/width
                // place the strip on the visible (cropped) area.
                double left, top, width;

                switch (rotation)
                {
                    case 90:
                        gfx.TranslateTransform(0, pageHeight);
                        gfx.RotateTransform(-90);
                        left = pageHeight - cropTop - cropHeight;
                        top = cropLeft;
                        width = cropHeight;
                        break;

                    case 180:
                        gfx.TranslateTransform(pageWidth, pageHeight);
                        gfx.RotateTransform(180);
                        left = pageWidth - cropLeft - cropWidth;
                        top = pageHeight - cropTop - cropHeight;
                        width = cropWidth;
                        break;

                    case 270:
                        gfx.TranslateTransform(pageWidth, 0);
                        gfx.RotateTransform(90);
                        left = cropTop;
                        top = pageWidth - cropLeft - cropWidth;
                        width = cropHeight;
                        break;

                    default:
                        left = cropLeft;
                        top = cropTop;
                        width = cropWidth;
                        break;
                }

                double available = Math.Max(20, width - 2 * SideMargin);
                XFont font = new XFont(FontName, FontSize, XFontStyle.Bold);

                // One line on an ordinary page; a narrow page (a receipt, a
                // phone screenshot) gets as many lines as the text needs.
                List<string> lines = Wrap(gfx, header, font, available);

                // A single item too long for even its own line: shrink the text.
                double size = FontSize;

                while (size > MinFontSize && lines.Any(l => gfx.MeasureString(l, font).Width > available))
                {
                    size -= 0.5;
                    font = new XFont(FontName, size, XFontStyle.Bold);
                }

                double stripHeight = lines.Count == 1
                    ? StripHeight
                    : lines.Count * LineHeight + 2 * LinePadding;

                gfx.DrawRectangle(XBrushes.White, new XRect(left, top, width, stripHeight));
                gfx.DrawLine(new XPen(XColors.Gray, 0.5),
                    left, top + stripHeight, left + width, top + stripHeight);

                for (int i = 0; i < lines.Count; i++)
                {
                    XRect line = lines.Count == 1
                        ? new XRect(left + SideMargin, top, available, StripHeight)
                        : new XRect(left + SideMargin, top + LinePadding + i * LineHeight, available, LineHeight);

                    gfx.DrawString(lines[i], font, XBrushes.Black, line, XStringFormats.CenterLeft);
                }
            }
        }

        /// <summary>
        /// Splits the header at its "|" separators into lines that fit the
        /// given width, keeping each item whole.
        /// </summary>
        private static List<string> Wrap(XGraphics gfx, string header, XFont font, double available)
        {
            const string separator = "  |  ";

            List<string> lines = new List<string>();
            string current = "";

            foreach (string part in header.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                string candidate = current.Length == 0 ? part : current + separator + part;

                if (current.Length > 0 && gfx.MeasureString(candidate, font).Width > available)
                {
                    lines.Add(current);
                    current = part;
                }
                else
                {
                    current = candidate;
                }
            }

            if (current.Length > 0)
                lines.Add(current);

            return lines;
        }
    }
}
