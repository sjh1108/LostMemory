using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.Networking;

namespace LostMemory.Tests
{
    /// <summary>
    /// 백엔드 1차 연동 검증 스크립트.
    /// signup → login → users/me 순서로 호출하면서 응답 매핑·DateTime·enum·한글 깨짐 모두 체크.
    /// </summary>
    public class LostMemoryApiTest : MonoBehaviour
    {
        [Header("Base URL (context-path /api 포함)")]
        [SerializeField] private string baseUrl = "http://localhost:8080/api";

        [Header("Test Account")]
        [SerializeField] private string loginId = "testuser";
        [SerializeField] private string password = "password123";
        [SerializeField] private string nickname = "테스터";

        private string accessToken;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            // enum 을 문자열("ACTIVE") 로 직렬화/역직렬화 (대소문자 무시)
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore
        };

        private void Start()
        {
            StartCoroutine(RunTests());
        }

        private IEnumerator RunTests()
        {
            Debug.Log("==== LostMemory API 1차 연동 테스트 시작 ====");

            yield return Signup();
            yield return Login();
            yield return GetMe();

            Debug.Log("==== 테스트 종료 ====");
        }

        // ============================================================
        // 1) signup — 한글 닉네임 깨짐 체크
        // ============================================================
        private IEnumerator Signup()
        {
            Debug.Log("--- [1] POST /auth/signup ---");
            var body = new SignupRequest
            {
                loginId = this.loginId,
                password = this.password,
                nickname = this.nickname
            };
            yield return PostJson<object>("/auth/signup", body, response =>
            {
                LogResponse("signup", response);
                if (response.success)
                    Debug.Log("✅ signup 성공");
                else if (response.error != null && response.error.code == "USER_LOGIN_ID_DUPLICATED")
                    Debug.Log("ℹ 이미 가입된 계정 — login 으로 진행");
                else
                    Debug.LogError($"❌ signup 실패: {response.error?.code}");
            });
        }

        // ============================================================
        // 2) login — accessToken 획득
        // ============================================================
        private IEnumerator Login()
        {
            Debug.Log("--- [2] POST /auth/login ---");
            var body = new LoginRequest
            {
                loginId = this.loginId,
                password = this.password
            };
            yield return PostJson<TokenResponse>("/auth/login", body, response =>
            {
                LogResponse("login", response);
                if (response.success && response.data != null)
                {
                    accessToken = response.data.accessToken;
                    Debug.Log($"✅ login 성공. accessToken 앞 20자: {accessToken[..20]}...");
                    Debug.Log($"   accessTokenExpiresIn: {response.data.accessTokenExpiresIn}s");
                }
                else
                {
                    Debug.LogError($"❌ login 실패: {response.error?.code}");
                }
            });
        }

        // ============================================================
        // 3) GET /users/me — Bearer 헤더 + DateTime/enum/한글 매핑 체크
        // ============================================================
        private IEnumerator GetMe()
        {
            Debug.Log("--- [3] GET /users/me ---");
            if (string.IsNullOrEmpty(accessToken))
            {
                Debug.LogError("❌ accessToken 없음. login 실패");
                yield break;
            }

            yield return GetJson<UserResponse>("/users/me", accessToken, response =>
            {
                LogResponse("users/me", response);
                if (!response.success || response.data == null)
                {
                    Debug.LogError($"❌ users/me 실패: {response.error?.code}");
                    return;
                }

                var u = response.data;
                Debug.Log($"✅ users/me 성공");
                Debug.Log($"   userId: {u.userId} (long 매핑 OK)");
                Debug.Log($"   loginId: {u.loginId}");
                Debug.Log($"   nickname: '{u.nickname}'  ← 한글 깨지는지 확인");
                Debug.Log($"   status: {u.status} (enum: {u.status.GetType().Name}.{u.status})");
                Debug.Log($"   createdAt: {u.createdAt:yyyy-MM-dd HH:mm:ss zzz} (DateTime 매핑 OK)");
                Debug.Log($"   lastLoginAt: {u.lastLoginAt?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "null"}");

                // 4가지 체크포인트 평가
                Debug.Log("===== 매핑 체크 =====");
                Debug.Log($"  [✓] ApiResponse<T> 매핑: success={response.success}");
                Debug.Log($"  [{(u.nickname == this.nickname ? "✓" : "✗")}] 한글 닉네임 라운드트립: 보낸값='{this.nickname}' 받은값='{u.nickname}'");
                Debug.Log($"  [{(u.status == UserStatus.ACTIVE ? "✓" : "✗")}] enum 매핑: status == ACTIVE");
                Debug.Log($"  [{(u.createdAt.Year >= 2026 ? "✓" : "✗")}] DateTime 파싱: createdAt 연도 {u.createdAt.Year}");
            });
        }

        // ============================================================
        // 공통 — POST/GET 헬퍼
        // ============================================================
        private IEnumerator PostJson<T>(string path, object body, Action<ApiResponse<T>> onComplete)
        {
            string url = baseUrl + path;
            string json = JsonConvert.SerializeObject(body, JsonSettings);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
            req.SetRequestHeader("Accept", "application/json");

            yield return req.SendWebRequest();

            string responseText = req.downloadHandler.text;
            Debug.Log($"  HTTP {(int)req.responseCode}, raw body: {responseText}");

            ApiResponse<T> parsed = ParseSafely<T>(responseText);
            onComplete(parsed);
        }

        private IEnumerator GetJson<T>(string path, string bearerToken, Action<ApiResponse<T>> onComplete)
        {
            string url = baseUrl + path;
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(bearerToken))
                req.SetRequestHeader("Authorization", "Bearer " + bearerToken);

            yield return req.SendWebRequest();

            string responseText = req.downloadHandler.text;
            Debug.Log($"  HTTP {(int)req.responseCode}, raw body: {responseText}");

            ApiResponse<T> parsed = ParseSafely<T>(responseText);
            onComplete(parsed);
        }

        private static ApiResponse<T> ParseSafely<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<ApiResponse<T>>(json, JsonSettings)
                    ?? new ApiResponse<T> { success = false };
            }
            catch (Exception e)
            {
                Debug.LogError($"  JSON 파싱 실패: {e.Message}");
                return new ApiResponse<T> { success = false };
            }
        }

        private static void LogResponse<T>(string label, ApiResponse<T> r)
        {
            Debug.Log($"  [{label}] success={r.success}, error.code={r.error?.code ?? "null"}");
        }
    }

    // ============================================================
    // 응답/요청 DTO — 백엔드와 1:1 매핑
    // ============================================================

    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public T data;
        public ErrorDetail error;
    }

    [Serializable]
    public class ErrorDetail
    {
        public string code;
        public string message;
    }

    [Serializable]
    public class SignupRequest
    {
        public string loginId;
        public string password;
        public string nickname;
    }

    [Serializable]
    public class LoginRequest
    {
        public string loginId;
        public string password;
    }

    [Serializable]
    public class TokenResponse
    {
        public string accessToken;
        public string refreshToken;
        public long accessTokenExpiresIn;
    }

    [Serializable]
    public class UserResponse
    {
        public long userId;
        public string loginId;
        public string nickname;
        public UserStatus status;
        public DateTime createdAt;
        public DateTime? lastLoginAt;
    }

    public enum UserStatus
    {
        ACTIVE,
        SUSPENDED,
        DELETED
    }
}