using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace KPSCObjections.Security
{
    /// <summary>
    /// Makes an uploaded PDF safe to open: removes everything that can run or
    /// act on its own when the file is opened, keeping only what is printed on
    /// the pages.
    ///
    /// The file is not repaired in place. A brand-new PDF is built and only the
    /// pages are copied into it, so the original's document-level parts are left
    /// behind: auto-run actions (/OpenAction), document JavaScript (/Names),
    /// attached files (/EmbeddedFiles), forms and XFA (/AcroForm), outlines and
    /// anything hidden after the end of the file (polyglots).
    ///
    /// From each page the annotations (links, form fields, file attachments,
    /// sound/movie/3D/RichMedia) and page actions (/AA) are removed. A final
    /// pass walks every object of the new file and strips any action or script
    /// key that is still reachable.
    /// </summary>
    public static class PdfSanitizer
    {
        public const int DefaultMaxPages = 50;

        /// <summary>Keys that start actions, scripts or embedded content.</summary>
        private static readonly string[] DangerousKeys =
        {
            "/AA", "/OpenAction", "/JS", "/JavaScript", "/Launch", "/URI",
            "/SubmitForm", "/ImportData", "/GoToR", "/GoToE", "/Movie", "/Sound",
            "/Rendition", "/RichMedia", "/RichMediaContent", "/EmbeddedFile",
            "/EmbeddedFiles", "/XFA", "/AcroForm", "/Names", "/Annots", "/A",
        };

        /// <summary>
        /// Page entries never copied into the clean document: annotations (links,
        /// form fields, attachments, media), page actions, article beads, and
        /// entries that only point at document structure left behind anyway.
        /// </summary>
        private static readonly string[] PageKeysNotCopied =
        {
            "/Annots", "/AA", "/B", "/StructParents", "/Tabs", "/Thumb", "/PieceInfo", "/Metadata"
        };

        public sealed class Result
        {
            public byte[] Pdf;

            /// <summary>What was found and removed, for logging (empty for a clean file).</summary>
            public List<string> Removed = new List<string>();
        }

        /// <summary>
        /// Returns a cleaned copy of the PDF. Throws when the file cannot be
        /// read (damaged, password-protected) or has more than
        /// <paramref name="maxPages"/> pages; the caller should refuse the file.
        /// </summary>
        public static Result Sanitize(byte[] pdf, int maxPages = DefaultMaxPages)
        {
            if (pdf == null || pdf.Length == 0)
                throw new InvalidDataException("The file is empty.");

            // An encrypted PDF cannot be cleaned: its content is unreadable
            // without the password. (PDFsharp 6 would open it anyway and copy the
            // still-encrypted content, producing a broken file.)
            if (IsEncrypted(pdf))
                throw new InvalidDataException("The PDF is password-protected or encrypted.");

            try
            {
                return Rebuild(pdf, maxPages);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception first)
            {
                // Some valid files make PDFsharp fail while copying pages into the
                // new document (seen with PDFs that were edited after saving, i.e.
                // incremental updates). Saving the file once through PDFsharp
                // rewrites it as one plain file; the copy then works. The result
                // is still rebuilt from its pages only, exactly as above.
                byte[] normalised;

                try
                {
                    normalised = Normalise(pdf);
                }
                catch
                {
                    throw first;
                }

                return Rebuild(normalised, maxPages);
            }
        }

        /// <summary>Opens the file for editing and saves it again, as one plain file.</summary>
        private static byte[] Normalise(byte[] pdf)
        {
            using (MemoryStream input = new MemoryStream(pdf))
            using (PdfDocument document = PdfReader.Open(input, PdfDocumentOpenMode.Modify))
            using (MemoryStream output = new MemoryStream())
            {
                document.Save(output, false);
                return output.ToArray();
            }
        }

        /// <summary>
        /// True when the file has an /Encrypt entry. It sits in the trailer or the
        /// cross-reference stream dictionary, which are never compressed, so it can
        /// be found in the raw bytes.
        /// </summary>
        private static bool IsEncrypted(byte[] pdf)
        {
            string text = System.Text.Encoding.GetEncoding(28591).GetString(pdf);
            int at = 0;

            while ((at = text.IndexOf("/Encrypt", at, StringComparison.Ordinal)) >= 0)
            {
                at += "/Encrypt".Length;

                // "/EncryptMetadata" and the like are other keys; /Encrypt itself is
                // followed by a delimiter (space, line break, "<<" or a reference).
                if (at >= text.Length || !char.IsLetterOrDigit(text[at]))
                    return true;
            }

            return false;
        }

        /// <summary>Builds the new, clean document from the pages of <paramref name="pdf"/>.</summary>
        private static Result Rebuild(byte[] pdf, int maxPages)
        {
            Result result = new Result();

            using (MemoryStream input = new MemoryStream(pdf))
            using (PdfDocument source = PdfReader.Open(input, PdfDocumentOpenMode.Import))
            using (PdfDocument clean = new PdfDocument())
            {
                if (source.PageCount == 0)
                    throw new InvalidDataException("The PDF has no pages.");

                if (source.PageCount > maxPages)
                    throw new InvalidDataException("The PDF has more than " + maxPages + " pages.");

                Note(result, source.Internals.Catalog, "document");

                foreach (PdfPage page in source.Pages)
                {
                    // Annotations, page actions and the rest are removed from the
                    // source page before it is copied, so the copy never follows
                    // their references at all. (Some edited PDFs have annotations
                    // pointing at objects that no longer exist, which made the copy
                    // fail.) The same keys are removed from the copy again below.
                    foreach (string key in PageKeysNotCopied)
                    {
                        if (page.Elements.ContainsKey(key))
                        {
                            page.Elements.Remove(key);

                            // Only active content is reported (and logged); the
                            // structural entries are harmless and just not copied.
                            if (key == "/Annots")
                                result.Removed.Add("page annotations");
                            else if (key == "/AA")
                                result.Removed.Add("page actions");
                            else if (key == "/B")
                                result.Removed.Add("page article beads");
                        }
                    }

                    PdfPage copy = clean.AddPage(page);

                    // Annotations and page actions are the page-level carriers
                    // of links, scripts, form fields and attached files.
                    Remove(copy.Elements, "/Annots", result, "page annotations");
                    Remove(copy.Elements, "/AA", result, "page actions");
                    Remove(copy.Elements, "/B", result, "page article beads");
                }

                // Belt and braces: nothing reachable in the new file may still
                // hold an action or script.
                HashSet<PdfObject> seen = NewSeenSet();

                Scrub(clean.Internals.Catalog, result, seen);

                foreach (PdfPage page in clean.Pages)
                    Scrub(page, result, seen);

                using (MemoryStream output = new MemoryStream())
                {
                    clean.Save(output, false);
                    result.Pdf = output.ToArray();
                }
            }

            result.Removed = result.Removed.Distinct().ToList();
            return result;
        }

        /// <summary>Records the document-level active content that is being left behind.</summary>
        private static void Note(Result result, PdfDictionary catalog, string where)
        {
            if (catalog == null)
                return;

            foreach (string key in new[] { "/OpenAction", "/AA", "/Names", "/AcroForm" })
            {
                if (catalog.Elements.ContainsKey(key))
                    result.Removed.Add(where + " " + key);
            }
        }

        private static void Remove(PdfDictionary.DictionaryElements elements, string key, Result result, string what)
        {
            if (elements.ContainsKey(key))
            {
                elements.Remove(key);
                result.Removed.Add(what);
            }
        }

        /// <summary>
        /// Walks a dictionary and everything it refers to, removing dangerous
        /// keys and action dictionaries (/S /JavaScript and the like).
        /// </summary>
        private static void Scrub(PdfItem item, Result result, HashSet<PdfObject> seen)
        {
            PdfReference reference = item as PdfReference;
            PdfObject obj = reference != null ? reference.Value : item as PdfObject;

            if (obj == null || !seen.Add(obj))
                return;

            PdfDictionary dict = obj as PdfDictionary;

            if (dict != null)
            {
                foreach (string key in dict.Elements.Keys.ToList())
                {
                    // Page tree links point back up; every page is scrubbed on its own.
                    if (key == "/Parent" || key == "/P")
                        continue;

                    if (DangerousKeys.Contains(key) && !IsHarmlessUse(dict, key))
                    {
                        dict.Elements.Remove(key);
                        result.Removed.Add(key);
                        continue;
                    }

                    Scrub(dict.Elements[key], result, seen);
                }

                return;
            }

            PdfArray array = obj as PdfArray;

            if (array != null)
            {
                foreach (PdfItem element in array.Elements.ToList())
                    Scrub(element, result, seen);
            }
        }

        /// <summary>Tracks objects by identity, so shared and circular references are visited once.</summary>
        private sealed class IdentityComparer : IEqualityComparer<PdfObject>
        {
            public bool Equals(PdfObject x, PdfObject y) { return ReferenceEquals(x, y); }
            public int GetHashCode(PdfObject obj) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj); }
        }

        private static HashSet<PdfObject> NewSeenSet()
        {
            return new HashSet<PdfObject>(new IdentityComparer());
        }

        /// <summary>
        /// /Names inside a font or colour space is unrelated to the document
        /// name tree (/Names there would hold JavaScript and attachments), and
        /// /A is only dangerous as an action. Keep those so pages still render.
        /// </summary>
        private static bool IsHarmlessUse(PdfDictionary dict, string key)
        {
            string type = dict.Elements.GetName("/Type");

            if (key == "/Names")
                return type != "/Catalog";

            if (key == "/A")
                return !(dict.Elements["/A"] is PdfDictionary || dict.Elements["/A"] is PdfReference);

            return false;
        }
    }
}
