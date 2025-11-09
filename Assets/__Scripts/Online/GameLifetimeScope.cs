// Filename: GameLifetimeScope.cs
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Unity.Netcode;
using MyGame.ConnectionSystem.Connection;
using MyGame.ConnectionSystem.Data;
using MyGame.ConnectionSystem.Services;
using MyGame.ConnectionSystem.States;

public class GameLifetimeScope : LifetimeScope
{
    [Header("Prefabs")]
    [SerializeField] private ConnectionManager connectionManagerPrefab;

    protected override void Configure(IContainerBuilder builder)
    {
        Debug.Log("--- GameLifetimeScope Configure method is running! ---");
        
        // --- 1. REGISTAR COMPONENTES DA CENA ---
        builder.RegisterComponentInHierarchy<UIManager>();
        builder.RegisterComponentInHierarchy<NetworkManager>();
        // Se tiver outros managers na cena, registe-os aqui
        // builder.RegisterComponentInHierarchy<EnemySpawner>();

        // --- 2. REGISTAR COMPONENTES A SEREM CRIADOS ---
        builder.RegisterComponentInNewPrefab(connectionManagerPrefab, Lifetime.Singleton).DontDestroyOnLoad();
        
        // --- 3. REGISTAR CLASSES C# PURAS ---
        builder.Register<ProfileManager>(Lifetime.Singleton);
        builder.Register<MultiplayerServicesFacade>(Lifetime.Singleton);
        builder.Register<AuthenticationServiceFacade>(Lifetime.Singleton);

        // --- 4. REGISTAR TODOS OS ESTADOS ---
        // O VContainer vai construir cada estado e injetar as suas dependências.
        builder.Register<OfflineState>(Lifetime.Singleton);
        builder.Register<StartingHostState>(Lifetime.Singleton);
        builder.Register<HostingState>(Lifetime.Singleton);
        builder.Register<ClientConnectingState>(Lifetime.Singleton);
        builder.Register<ClientConnectedState>(Lifetime.Singleton);
        builder.Register<ClientReconnectingState>(Lifetime.Singleton);
    }
}