using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LostMemory.Dialogue
{
    /// <summary>
    /// dialogues.csv 를 로드/파싱/캐시하는 ScriptableObject.
    ///
    /// 사용법:
    ///   1. Project 우클릭 → Create → LostMemory/Dialogue/Dialogue Database 로 asset 생성
    ///   2. inspector 의 csvAsset 슬롯에 dialogues.csv (TextAsset) 드래그
    ///   3. DialogueController 가 BuildIfNeeded() 호출 후 TryGetGroup(prefix) 로 조회
    ///
    /// CSV 포맷 (RFC 4180, RelicDataBatchGenerator 패턴 차용):
    ///   id,speaker,text,portraitId,isPlayer
    ///   boss_intro_1,Khi,"(기분 탓인가?...)",khi_neutral,true
    ///
    /// 그룹화 규칙: id 의 마지막 "_숫자" 토큰을 떼어 groupId 로 사용.
    ///   "boss_intro_3" → groupId="boss_intro", order=3
    /// 순서번호는 1부터 연속 권장 (빈 번호는 경고 로그).
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "LostMemory/Dialogue/Dialogue Database")]
    public sealed class DialogueDatabase : ScriptableObject
    {
        [Header("Source")]
        [Tooltip("dialogues.csv 를 드래그. 헤더: id,speaker,text,portraitId,isPlayer")]
        [SerializeField] private TextAsset csvAsset;

        [Header("Options")]
        [SerializeField] private bool logOnBuild = true;

        // groupId → ordered lines
        private readonly Dictionary<string, DialogueGroup> _groups = new Dictionary<string, DialogueGroup>();
        private bool _built;

        public bool IsBuilt => _built;
        public TextAsset CsvAsset => csvAsset;

        /// <summary>
        /// 최초 1회 또는 ForceRebuild 호출 시 csv 파싱.
        /// </summary>
        public void BuildIfNeeded()
        {
            if (_built) return;
            Build();
        }

        public void ForceRebuild()
        {
            _built = false;
            _groups.Clear();
            Build();
        }

        private void Build()
        {
            _groups.Clear();
            if (csvAsset == null)
            {
                Debug.LogError("[DialogueDatabase] csvAsset 가 null. inspector 에서 dialogues.csv 를 할당하세요.", this);
                _built = true;
                return;
            }

            List<DialogueLine> allLines = ParseCsv(csvAsset.text);

            // groupId 별로 묶기
            var groupBuckets = new Dictionary<string, List<DialogueLine>>();
            foreach (DialogueLine line in allLines)
            {
                if (!groupBuckets.TryGetValue(line.GroupId, out List<DialogueLine> bucket))
                {
                    bucket = new List<DialogueLine>();
                    groupBuckets[line.GroupId] = bucket;
                }
                bucket.Add(line);
            }

            foreach (KeyValuePair<string, List<DialogueLine>> kv in groupBuckets)
            {
                List<DialogueLine> bucket = kv.Value;
                bucket.Sort((a, b) => a.Order.CompareTo(b.Order));
                ValidateContinuity(kv.Key, bucket);
                _groups[kv.Key] = new DialogueGroup(kv.Key, bucket);
            }

            _built = true;

            if (logOnBuild)
            {
                Debug.Log($"[DialogueDatabase] Built — {allLines.Count} lines, {_groups.Count} groups: " +
                          string.Join(", ", _groups.Keys), this);
            }
        }

        public bool TryGetGroup(string groupId, out DialogueGroup group)
        {
            BuildIfNeeded();
            return _groups.TryGetValue(groupId, out group);
        }

        public IReadOnlyDictionary<string, DialogueGroup> AllGroups
        {
            get
            {
                BuildIfNeeded();
                return _groups;
            }
        }

        // === CSV 파싱 (RFC 4180: 따옴표 안의 콤마/줄바꿈/이스케이프된 ""허용) ============

        private static List<DialogueLine> ParseCsv(string raw)
        {
            var rows = SplitRows(raw);
            var result = new List<DialogueLine>();
            if (rows.Count == 0) return result;

            // 첫 행 = 헤더. 컬럼 인덱스 매핑.
            List<string> header = SplitRow(rows[0]);
            int idxId = header.IndexOf("id");
            int idxSpeaker = header.IndexOf("speaker");
            int idxText = header.IndexOf("text");
            int idxPortrait = header.IndexOf("portraitId");
            int idxIsPlayer = header.IndexOf("isPlayer");

            if (idxId < 0 || idxSpeaker < 0 || idxText < 0 || idxPortrait < 0 || idxIsPlayer < 0)
            {
                Debug.LogError("[DialogueDatabase] CSV 헤더가 누락됨. 필요: id, speaker, text, portraitId, isPlayer");
                return result;
            }

            for (int r = 1; r < rows.Count; r++)
            {
                string rowText = rows[r];
                if (string.IsNullOrWhiteSpace(rowText)) continue;

                List<string> cols = SplitRow(rowText);
                if (cols.Count <= idxIsPlayer)
                {
                    Debug.LogWarning($"[DialogueDatabase] row {r + 1} 컬럼 수 부족 — 건너뜀: {rowText}");
                    continue;
                }

                string id = cols[idxId].Trim();
                if (string.IsNullOrEmpty(id)) continue;

                if (!TrySplitGroupAndOrder(id, out string groupId, out int order))
                {
                    Debug.LogWarning($"[DialogueDatabase] id 형식 오류 (끝이 '_숫자' 여야 함) — 건너뜀: '{id}'");
                    continue;
                }

                bool isPlayer = ParseBool(cols[idxIsPlayer]);

                result.Add(new DialogueLine(
                    id: id,
                    groupId: groupId,
                    order: order,
                    speaker: cols[idxSpeaker].Trim(),
                    text: cols[idxText],
                    portraitId: cols[idxPortrait].Trim(),
                    isPlayer: isPlayer));
            }

            return result;
        }

        private static bool TrySplitGroupAndOrder(string id, out string groupId, out int order)
        {
            int underscore = id.LastIndexOf('_');
            if (underscore <= 0 || underscore >= id.Length - 1)
            {
                groupId = id; order = 0; return false;
            }

            string tail = id.Substring(underscore + 1);
            if (!int.TryParse(tail, out order))
            {
                groupId = id; order = 0; return false;
            }

            groupId = id.Substring(0, underscore);
            return true;
        }

        private static bool ParseBool(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            string trimmed = s.Trim().ToLowerInvariant();
            return trimmed == "true" || trimmed == "1" || trimmed == "yes" || trimmed == "y";
        }

        private void ValidateContinuity(string groupId, List<DialogueLine> sorted)
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                int expected = i + 1;
                if (sorted[i].Order != expected)
                {
                    Debug.LogWarning(
                        $"[DialogueDatabase] 그룹 '{groupId}' 순서번호 비연속 — index {i}, expected {expected}, got {sorted[i].Order} (id='{sorted[i].Id}'). " +
                        "순서번호는 1부터 연속이어야 합니다.", this);
                }
            }
        }

        // RFC 4180: 따옴표 안의 줄바꿈을 한 row 로 묶기 위해 char-by-char 스캔.
        private static List<string> SplitRows(string text)
        {
            var rows = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"')
                {
                    // "" 이스케이프: 안에 있는 따옴표 두 개
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        sb.Append('"').Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                        sb.Append(c);
                    }
                    continue;
                }

                if (!inQuotes && (c == '\n' || c == '\r'))
                {
                    // CRLF 의 LF 는 같은 줄바꿈으로 묶음
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    rows.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }
            if (sb.Length > 0) rows.Add(sb.ToString());
            return rows;
        }

        // 한 row 내에서 따옴표 안의 콤마를 보존하면서 split.
        private static List<string> SplitRow(string row)
        {
            var cols = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < row.Length; i++)
            {
                char c = row[i];
                if (c == '"')
                {
                    // "" 이스케이프
                    if (inQuotes && i + 1 < row.Length && row[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    cols.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }
            cols.Add(sb.ToString());
            return cols;
        }
    }
}
