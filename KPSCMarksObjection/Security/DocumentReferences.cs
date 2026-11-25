using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;
using System.Web.SessionState;

namespace KPSCMarksObjection.Security
{
    /// <summary>
    /// Indirect references for supporting documents (protection against
    /// Insecure Direct Object Reference).
    ///
    /// Links never carry the paper code and question number. Each link gets a
    /// random token that is valid only in the session that showed it; the
    /// token maps to the document's paper and question on the server. Editing
    /// or guessing a token, or using one from another session, finds nothing.
    /// MarksDocument.ashx additionally reads the registration number from
    /// the session, so a token can never point at another candidate's file.
    /// </summary>
    public static class DocumentReferences
    {
        private const string SessionKey = "DocumentReferences";

        /// <summary>Upper bound on tokens kept per session (a candidate has far fewer documents).</summary>
        private const int MaxPerSession = 1000;

        public sealed class Target
        {
            public string PaperCode;
            public int QuestionNo;
        }

        /// <summary>
        /// Paper codes of marks objection documents carry this prefix in the
        /// map; MarksDocument.ashx resolves only tokens that have it.
        /// </summary>
        public const string MarksPrefix = "M:";

        /// <summary>The link URL for one of the signed-in candidate's marks objection documents.</summary>
        public static string MarksUrl(HttpSessionState session, object paperCode)
        {
            return "~/MarksDocument.ashx?d=" + Token(session, MarksPrefix + Convert.ToString(paperCode), 0);
        }

        /// <summary>The document a token stands for in this session, or null.</summary>
        public static Target Resolve(HttpSessionState session, string token)
        {
            Dictionary<string, Target> map = session[SessionKey] as Dictionary<string, Target>;
            Target target;

            if (map == null || string.IsNullOrEmpty(token) || !map.TryGetValue(token, out target))
                return null;

            return target;
        }

        private static string Token(HttpSessionState session, string paperCode, int questionNo)
        {
            Dictionary<string, Target> map = session[SessionKey] as Dictionary<string, Target>;

            if (map == null)
            {
                map = new Dictionary<string, Target>(StringComparer.Ordinal);
                session[SessionKey] = map;
            }

            // The same document keeps the same token within a session.
            foreach (KeyValuePair<string, Target> entry in map)
            {
                if (entry.Value.QuestionNo == questionNo &&
                    string.Equals(entry.Value.PaperCode, paperCode, StringComparison.Ordinal))
                {
                    return entry.Key;
                }
            }

            if (map.Count >= MaxPerSession)
                map.Clear();

            string token = NewToken();
            map[token] = new Target { PaperCode = paperCode, QuestionNo = questionNo };

            return token;
        }

        private static string NewToken()
        {
            byte[] bytes = new byte[18];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_');
        }
    }
}
