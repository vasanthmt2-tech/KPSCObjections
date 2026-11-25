using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace KPSCExamPortal
{
    public class ObjectionSession
    {
        /// <summary>The one candidate sign-in (OTP to the mobile number in dbo.MarksCandidate).</summary>
        public const string KeySource = "Candidate";

        public struct LoginDetails
        {
            /// <summary>Always KeySource: kept so a sign-in token names its candidate list.</summary>
            public string Source;

            public string Name;
            public string Dob;
            public string RegNo;
            public string ExamCode;
        }
    }
}