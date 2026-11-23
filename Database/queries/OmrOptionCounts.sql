-- How many candidates chose each option (1 to 5), question by question,
-- for Paper I (P1) and Paper II (P2), from dbo.OmrResult.
--
--   Option5 = the 5th circle (not attempted), Blank = nothing marked,
--   Other   = anything else the scanner reported (for example two circles).
--
-- Question numbers differ between versions, so there are two queries:
--   1. By MASTER question: each candidate's question number is mapped to the
--      master question through the final key (dbo.OmrKeyAnswers, Set1..Set12
--      by position: P1 sets A..L, P2 sets M..X). Needs the final key uploaded.
--   2. By question number as printed in each VERSION (no key needed).
-- Run either one on its own (select it in SSMS and press F5).

-- 1. By master question (all versions of a paper counted together) -------
;WITH Answers AS (
    SELECT r.Paper, r.Version, a.QuestionNo, NULLIF(LTRIM(RTRIM(a.Answer)), '') AS Answer
    FROM dbo.OmrResult r
    CROSS APPLY (VALUES
        (1, r.Q1), (2, r.Q2), (3, r.Q3), (4, r.Q4), (5, r.Q5), (6, r.Q6), (7, r.Q7), (8, r.Q8), (9, r.Q9), (10, r.Q10),
        (11, r.Q11), (12, r.Q12), (13, r.Q13), (14, r.Q14), (15, r.Q15), (16, r.Q16), (17, r.Q17), (18, r.Q18), (19, r.Q19), (20, r.Q20),
        (21, r.Q21), (22, r.Q22), (23, r.Q23), (24, r.Q24), (25, r.Q25), (26, r.Q26), (27, r.Q27), (28, r.Q28), (29, r.Q29), (30, r.Q30),
        (31, r.Q31), (32, r.Q32), (33, r.Q33), (34, r.Q34), (35, r.Q35), (36, r.Q36), (37, r.Q37), (38, r.Q38), (39, r.Q39), (40, r.Q40),
        (41, r.Q41), (42, r.Q42), (43, r.Q43), (44, r.Q44), (45, r.Q45), (46, r.Q46), (47, r.Q47), (48, r.Q48), (49, r.Q49), (50, r.Q50),
        (51, r.Q51), (52, r.Q52), (53, r.Q53), (54, r.Q54), (55, r.Q55), (56, r.Q56), (57, r.Q57), (58, r.Q58), (59, r.Q59), (60, r.Q60),
        (61, r.Q61), (62, r.Q62), (63, r.Q63), (64, r.Q64), (65, r.Q65), (66, r.Q66), (67, r.Q67), (68, r.Q68), (69, r.Q69), (70, r.Q70),
        (71, r.Q71), (72, r.Q72), (73, r.Q73), (74, r.Q74), (75, r.Q75), (76, r.Q76), (77, r.Q77), (78, r.Q78), (79, r.Q79), (80, r.Q80),
        (81, r.Q81), (82, r.Q82), (83, r.Q83), (84, r.Q84), (85, r.Q85), (86, r.Q86), (87, r.Q87), (88, r.Q88), (89, r.Q89), (90, r.Q90),
        (91, r.Q91), (92, r.Q92), (93, r.Q93), (94, r.Q94), (95, r.Q95), (96, r.Q96), (97, r.Q97), (98, r.Q98), (99, r.Q99), (100, r.Q100)
    ) a(QuestionNo, Answer)
),
KeyMap AS (
    -- Master question for each version's question number.
    SELECT ka.SubjectCode AS Paper, v.Version, TRY_CONVERT(int, v.VersionQuestionNo) AS VersionQuestionNo,
           ka.QuestionNo AS MasterQuestionNo
    FROM dbo.OmrKeyAnswers ka
    CROSS APPLY (VALUES
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 0),  ka.Set1),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 1),  ka.Set2),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 2),  ka.Set3),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 3),  ka.Set4),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 4),  ka.Set5),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 5),  ka.Set6),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 6),  ka.Set7),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 7),  ka.Set8),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 8),  ka.Set9),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 9),  ka.Set10),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 10), ka.Set11),
        (CHAR(ASCII(CASE ka.SubjectCode WHEN 'P2' THEN 'M' ELSE 'A' END) + 11), ka.Set12)
    ) v(Version, VersionQuestionNo)
    WHERE v.VersionQuestionNo IS NOT NULL
)
SELECT a.Paper,
       m.MasterQuestionNo,
       SUM(CASE WHEN a.Answer = '1' THEN 1 ELSE 0 END) AS Option1,
       SUM(CASE WHEN a.Answer = '2' THEN 1 ELSE 0 END) AS Option2,
       SUM(CASE WHEN a.Answer = '3' THEN 1 ELSE 0 END) AS Option3,
       SUM(CASE WHEN a.Answer = '4' THEN 1 ELSE 0 END) AS Option4,
       SUM(CASE WHEN a.Answer = '5' THEN 1 ELSE 0 END) AS Option5,
       SUM(CASE WHEN a.Answer IS NULL OR LTRIM(RTRIM(a.Answer)) = '' THEN 1 ELSE 0 END) AS Blank,
       SUM(CASE WHEN a.Answer NOT IN ('1', '2', '3', '4', '5') AND LTRIM(RTRIM(a.Answer)) <> '' THEN 1 ELSE 0 END) AS Other,
       COUNT(*) AS Total
FROM Answers a
INNER JOIN KeyMap m
        ON m.Paper = a.Paper AND m.Version = a.Version AND m.VersionQuestionNo = a.QuestionNo
GROUP BY a.Paper, m.MasterQuestionNo
ORDER BY a.Paper, m.MasterQuestionNo;


-- 2. By question number as printed in each version --------------------------
;WITH Answers AS (
    SELECT r.Paper, r.Version, a.QuestionNo, NULLIF(LTRIM(RTRIM(a.Answer)), '') AS Answer
    FROM dbo.OmrResult r
    CROSS APPLY (VALUES
        (1, r.Q1), (2, r.Q2), (3, r.Q3), (4, r.Q4), (5, r.Q5), (6, r.Q6), (7, r.Q7), (8, r.Q8), (9, r.Q9), (10, r.Q10),
        (11, r.Q11), (12, r.Q12), (13, r.Q13), (14, r.Q14), (15, r.Q15), (16, r.Q16), (17, r.Q17), (18, r.Q18), (19, r.Q19), (20, r.Q20),
        (21, r.Q21), (22, r.Q22), (23, r.Q23), (24, r.Q24), (25, r.Q25), (26, r.Q26), (27, r.Q27), (28, r.Q28), (29, r.Q29), (30, r.Q30),
        (31, r.Q31), (32, r.Q32), (33, r.Q33), (34, r.Q34), (35, r.Q35), (36, r.Q36), (37, r.Q37), (38, r.Q38), (39, r.Q39), (40, r.Q40),
        (41, r.Q41), (42, r.Q42), (43, r.Q43), (44, r.Q44), (45, r.Q45), (46, r.Q46), (47, r.Q47), (48, r.Q48), (49, r.Q49), (50, r.Q50),
        (51, r.Q51), (52, r.Q52), (53, r.Q53), (54, r.Q54), (55, r.Q55), (56, r.Q56), (57, r.Q57), (58, r.Q58), (59, r.Q59), (60, r.Q60),
        (61, r.Q61), (62, r.Q62), (63, r.Q63), (64, r.Q64), (65, r.Q65), (66, r.Q66), (67, r.Q67), (68, r.Q68), (69, r.Q69), (70, r.Q70),
        (71, r.Q71), (72, r.Q72), (73, r.Q73), (74, r.Q74), (75, r.Q75), (76, r.Q76), (77, r.Q77), (78, r.Q78), (79, r.Q79), (80, r.Q80),
        (81, r.Q81), (82, r.Q82), (83, r.Q83), (84, r.Q84), (85, r.Q85), (86, r.Q86), (87, r.Q87), (88, r.Q88), (89, r.Q89), (90, r.Q90),
        (91, r.Q91), (92, r.Q92), (93, r.Q93), (94, r.Q94), (95, r.Q95), (96, r.Q96), (97, r.Q97), (98, r.Q98), (99, r.Q99), (100, r.Q100)
    ) a(QuestionNo, Answer)
)
SELECT a.Paper,
       a.Version,
       a.QuestionNo,
       SUM(CASE WHEN a.Answer = '1' THEN 1 ELSE 0 END) AS Option1,
       SUM(CASE WHEN a.Answer = '2' THEN 1 ELSE 0 END) AS Option2,
       SUM(CASE WHEN a.Answer = '3' THEN 1 ELSE 0 END) AS Option3,
       SUM(CASE WHEN a.Answer = '4' THEN 1 ELSE 0 END) AS Option4,
       SUM(CASE WHEN a.Answer = '5' THEN 1 ELSE 0 END) AS Option5,
       SUM(CASE WHEN a.Answer IS NULL OR LTRIM(RTRIM(a.Answer)) = '' THEN 1 ELSE 0 END) AS Blank,
       SUM(CASE WHEN a.Answer NOT IN ('1', '2', '3', '4', '5') AND LTRIM(RTRIM(a.Answer)) <> '' THEN 1 ELSE 0 END) AS Other,
       COUNT(*) AS Total
FROM Answers a
GROUP BY a.Paper, a.Version, a.QuestionNo
ORDER BY a.Paper, a.Version, a.QuestionNo;
