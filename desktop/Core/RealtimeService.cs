using System.IO;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;

namespace SmartRestaurant.Desktop.Core;

/// <summary>SignalR client: /hubs/notifications + /hubs/kds — pushes into UI events.</summary>
public class RealtimeService
{
    private readonly DesktopSettings _settings;
    public HubConnection? Notifications { get; private set; }
    public HubConnection? Kds { get; private set; }
    public bool IsConnected => Notifications?.State == HubConnectionState.Connected;

    public event Action<NotificationDto>? NotificationReceived;
    public event Action<OrderDto>? OrderUpdated;
    public event Action<string>? StatsRefresh;
    public event Action<PaymentDto>? PaymentCaptured;
    public event Action<string>? ConnectionChanged;
    public event Action<KdsTicketDto>? TicketCreated;
    public event Action<KdsTicketDto>? TicketUpdated;
    public event Action<Guid>? TicketRemoved;
    public event Action<Guid, string, string>? OrderStatusChanged;

    public RealtimeService(DesktopSettings settings) => _settings = settings;

    public async Task ConnectAsync(string accessToken, Guid? branchId, int role)
    {
        await DisconnectAsync();
        var url = _settings.ServerUrl.TrimEnd('/');

        Notifications = new HubConnectionBuilder()
            .WithUrl($"{url}/hubs/notifications", o => o.AccessTokenProvider = () => Task.FromResult<string?>(accessToken))
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15) })
            .Build();
        Kds = new HubConnectionBuilder()
            .WithUrl($"{url}/hubs/kds", o => o.AccessTokenProvider = () => Task.FromResult<string?>(accessToken))
            .WithAutomaticReconnect()
            .Build();

        Notifications.On<NotificationDto>("ReceiveNotification", n => Safe(() => NotificationReceived?.Invoke(n)));
        Notifications.On<OrderDto>("OrderUpdated", o => Safe(() => OrderUpdated?.Invoke(o)));
        Notifications.On<string>("StatsRefresh", r => Safe(() => StatsRefresh?.Invoke(r)));
        Notifications.On<PaymentDto>("PaymentCaptured", p => Safe(() => PaymentCaptured?.Invoke(p)));
        Kds.On<KdsTicketDto>("TicketCreated", t => Safe(() => TicketCreated?.Invoke(t)));
        Kds.On<KdsTicketDto>("TicketUpdated", t => Safe(() => TicketUpdated?.Invoke(t)));
        Kds.On<Guid>("TicketRemoved", id => Safe(() => TicketRemoved?.Invoke(id)));
        Kds.On<Guid, string, string>("OrderStatusChanged", (a, b, c) => Safe(() => OrderStatusChanged?.Invoke(a, b, c)));

        Notifications.Closed += _ => { Safe(() => ConnectionChanged?.Invoke("offline")); return Task.CompletedTask; };
        Notifications.Reconnected += _ => { Safe(() => ConnectionChanged?.Invoke("online")); JoinGroupsAsync(); return Task.CompletedTask; };

        await Notifications.StartAsync();
        await Kds.StartAsync();
        JoinGroupsAsync();
        Safe(() => ConnectionChanged?.Invoke("online"));
    }

    private void JoinGroupsAsync()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (Session.BranchId != null) await Notifications!.InvokeAsync("JoinBranch", Session.BranchId.ToString());
                await Notifications!.InvokeAsync("JoinRole", (int)Session.User!.Role);
                await Notifications!.InvokeAsync("JoinUser", Session.User!.Id.ToString());
                if (Session.BranchId != null) await Kds!.InvokeAsync("JoinBranch", Session.BranchId.ToString());
            }
            catch { }
        });
    }

    public async Task DisconnectAsync()
    {
        if (Notifications != null) try { await Notifications.DisposeAsync(); } catch { }
        if (Kds != null) try { await Kds.DisposeAsync(); } catch { }
        Notifications = null; Kds = null;
    }

    private static void Safe(Action action)
    {
        var app = Application.Current;
        if (app?.Dispatcher.CheckAccess() == true) action();
        else app?.Dispatcher.BeginInvoke(action);
    }
}

/// <summary>Dependency-free UI sounds (KDS new-ticket chime) generated as WAV bytes.</summary>
public class AudioService
{
    private static System.Media.SoundPlayer? _chime;
    private static System.Media.SoundPlayer? _alert;

    public AudioService()
    {
        _chime = new System.Media.SoundPlayer(new MemoryStream(MakeTone(880, 0.12, 0.6)));
        _chime.Load();
        _alert = new System.Media.SoundPlayer(new MemoryStream(MakeTone(660, 0.08, 0.5)));
        _alert.Load();
    }

    public void Chime() => _chime?.Play();
    public void Alert() => _alert?.Play();

    private static byte[] MakeTone(double freq, double seconds, double gain)
    {
        int rate = 22050;
        int n = (int)(rate * seconds);
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            w.Write("RIFF".ToCharArray()); w.Write(36 + n * 2); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(n * 2);
            for (int i = 0; i < n; i++)
            {
                var envelope = 1.0 - (double)i / n;
                short v = (short)(Math.Sin(2 * Math.PI * freq * i / rate) * 32767 * gain * envelope);
                w.Write(v);
            }
        }
        return ms.ToArray();
    }
}
