using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace LestingCraft.Networking
{
    /// <summary>One published world returned by GET /api/servers.</summary>
    [Serializable]
    public sealed class PublicServer
    {
        public string code, name;
        public int mode, seed, dimension, rulesVersion, contentVersion, players, maxPlayers;
        public long createdAt;
        public bool started, offline;

        public string PopulationText { get { return (offline ? "не в сети · " : string.Empty) + players + "/" + (maxPlayers > 0 ? maxPlayers : 8); } }
    }

    [Serializable] sealed class ServerListResponse { public string type; public PublicServer[] servers; }
    [Serializable] sealed class ServerOneResponse { public string type, code; public PublicServer server; }
    [Serializable] sealed class ServerPublishRequest
    {
        public int version = 1, rulesVersion, dimension, mode, seed;
        public string ownerKey, name;
    }

    /// <summary>
    /// Thin HTTP client for the public server directory: list the worlds hosted on
    /// an address, publish a world from this machine's game, unpublish it again.
    /// </summary>
    public sealed class ServerBrowser : MonoBehaviour
    {
        public void List(string baseUrl, Action<PublicServer[]> done, Action<string> failed)
        {
            StartCoroutine(ListRoutine(Trim(baseUrl) + "/api/servers", done, failed));
        }

        public void Publish(string baseUrl, string serverName, int seed, int mode, string ownerKey, Action<PublicServer> done, Action<string> failed)
        {
            var body = new ServerPublishRequest
            {
                rulesVersion = LestingCraft.Gameplay.GameRules.Version,
                dimension = 0, mode = mode, seed = seed,
                ownerKey = ownerKey, name = serverName,
            };
            StartCoroutine(PublishRoutine(Trim(baseUrl) + "/api/servers", JsonUtility.ToJson(body), done, failed));
        }

        public void Remove(string baseUrl, string code, string ownerKey, Action done, Action<string> failed)
        {
            StartCoroutine(RemoveRoutine(Trim(baseUrl) + "/api/servers/" + code + "?owner=" + Uri.EscapeDataString(ownerKey ?? string.Empty), done, failed));
        }

        static string Trim(string url) { return (url ?? string.Empty).TrimEnd('/'); }

        IEnumerator ListRoutine(string url, Action<PublicServer[]> done, Action<string> failed)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 12;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { Fail(failed, request); yield break; }
                ServerListResponse parsed = null;
                try { parsed = JsonUtility.FromJson<ServerListResponse>(request.downloadHandler.text); }
                catch (Exception) { if (failed != null) failed("Сервер вернул нечитаемый список миров"); yield break; }
                if (parsed == null || parsed.servers == null) { if (failed != null) failed("Сервер не вернул список миров"); yield break; }
                if (done != null) done(parsed.servers);
            }
        }

        IEnumerator PublishRoutine(string url, string body, Action<PublicServer> done, Action<string> failed)
        {
            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 20;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { Fail(failed, request); yield break; }
                ServerOneResponse parsed = null;
                try { parsed = JsonUtility.FromJson<ServerOneResponse>(request.downloadHandler.text); }
                catch (Exception) { if (failed != null) failed("Сервер не подтвердил создание мира"); yield break; }
                if (parsed == null || parsed.server == null || string.IsNullOrEmpty(parsed.code))
                { if (failed != null) failed("Сервер не подтвердил создание мира"); yield break; }
                if (done != null) done(parsed.server);
            }
        }

        IEnumerator RemoveRoutine(string url, Action done, Action<string> failed)
        {
            using (var request = UnityWebRequest.Delete(url))
            {
                request.timeout = 12;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { Fail(failed, request); yield break; }
                if (done != null) done();
            }
        }

        static void Fail(Action<string> failed, UnityWebRequest request)
        {
            if (failed == null) return;
            long code = request.responseCode;
            if (code == 404) failed("Сервер не найден по этому адресу");
            else if (code == 429) failed("Слишком много миров за последний час — попробуйте позже");
            else if (code > 0) failed("Ошибка сервера " + code);
            else failed("Нет связи с сервером: " + request.error);
        }
    }
}
