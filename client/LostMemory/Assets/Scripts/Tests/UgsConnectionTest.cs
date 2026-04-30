using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Multiplayer;
using UnityEngine;

public class UgsConnectionTest : MonoBehaviour
{
    private void Awake()
    {
        if (NetworkManager.Singleton == null)
        {
            var go = new GameObject("NetworkManager");
            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport
            };
        }
    }

    private async void Start()
    {
        try
        {
            var options = new InitializationOptions();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            options.SetEnvironmentName("development");
#else
              options.SetEnvironmentName("production");
#endif
            await UnityServices.InitializeAsync(options);
            Debug.Log("[UGS] InitializeAsync OK");

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[UGS] SignedIn. playerId={AuthenticationService.Instance.PlayerId}");

            var sessionOptions = new SessionOptions
            {
                MaxPlayers = 3,
                IsPrivate = false
            }.WithRelayNetwork();

            var session = await MultiplayerService.Instance.CreateSessionAsync(sessionOptions);
            Debug.Log($"[UGS] Session created. id={session.Id}, code={session.Code},host ={ session.IsHost}");

              await session.LeaveAsync();
            Debug.Log("[UGS] ✅ All checks passed.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UGS] ❌ FAILED: {e}");
        }
    }
}