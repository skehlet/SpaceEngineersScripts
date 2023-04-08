string MyName = null;
IMyRadioAntenna antenna = null;
Dictionary<string, List<Action<MyIGCMessage>>> Handlers = new Dictionary<string, List<Action<MyIGCMessage>>>();
string UNICAST = "UNICAST";
string BROADCAST = "BROADCAST";

T GetFirstBlockOfType<T>() where T : class, IMyTerminalBlock
{
	var blocks = new List<T>();
	GridTerminalSystem.GetBlocksOfType(blocks);
	foreach (var block in blocks)
	{
		if (block.IsSameConstructAs(Me)) return block;
	}
	return null;
}

Vector3D GetMyGps()
{
	return antenna.GetPosition();
}

void InitUnicastListener()
{
	IGC.UnicastListener.SetMessageCallback(UNICAST);
}

void RegisterHandler(string tag, Action<MyIGCMessage> handler)
{
	if (!Handlers.ContainsKey(tag))
	{
		Handlers[tag] = new List<Action<MyIGCMessage>>();
	}
	Handlers[tag].Add(handler);
}

void HandleBroadcastIGCMessage(string tag, Action<MyIGCMessage> handler)
{
	IGC.RegisterBroadcastListener(tag).SetMessageCallback(BROADCAST);
	RegisterHandler(tag, handler);
}

void HandleUnicastIGCMessage(string tag, Action<MyIGCMessage> handler)
{
	RegisterHandler(tag, handler);
}

public Program()
{
	antenna = GetFirstBlockOfType<IMyRadioAntenna>();
	if (antenna == null) return;

	MyName = IGC.Me.ToString();
	InitUnicastListener();
	
	HandleBroadcastIGCMessage("PING", (MyIGCMessage message) => {
		Echo("Received PING from: " + message.Source + " with gps: " + (Vector3D)message.Data);
		SendPong(message.Source);
	});

	HandleUnicastIGCMessage("PONG", (MyIGCMessage message) => {
		Echo("Received PONG from: " + message.Source + " with gps: " + (Vector3D)message.Data);
	});
	
	Echo("PING/PONG started for id: " + MyName);
}

public void SendPing()
{
	var gps = GetMyGps();
	Echo("Broadcasting PING with my gps: " + gps);
	IGC.SendBroadcastMessage(
		"PING", // tag
		gps,    // data
		TransmissionDistance.TransmissionDistanceMax // distance
	);
}

public void SendPong(long id)
{
	var gps = GetMyGps();
	Echo("Unicasting PONG back to " + id + " with my gps: " + gps);
	IGC.SendUnicastMessage(
		id,        // recipient
		"PONG",    // tag
		gps        // data
	);
}

void HandleMessages(IMyMessageProvider provider)
{
	while (provider.HasPendingMessage)
	{
		var message = provider.AcceptMessage();
		if (Handlers.ContainsKey(message.Tag))
		{
			var handlers = Handlers[message.Tag];
			foreach (var handler in handlers)
			{
				handler(message);
			}
		}
		else
		{
			Echo("No handler for message: " + message);
		}
	}
}

public void Main(string argument, UpdateType updateSource)
{
	Echo("updateSource: " + updateSource + ", argument: " + argument);

	try {
		if (updateSource == UpdateType.Terminal || updateSource == UpdateType.Trigger)
		{
			SendPing();
		}
		else if (updateSource == UpdateType.IGC)
		{
			if (argument == UNICAST)
			{
				HandleMessages(IGC.UnicastListener);
			}
			else if (argument == BROADCAST)
			{
				List<IMyBroadcastListener> broadcastListeners = new List<IMyBroadcastListener>();
				IGC.GetBroadcastListeners(broadcastListeners, (listener) => listener.HasPendingMessage);
				foreach (var listener in broadcastListeners)
				{
					HandleMessages(listener);
				}
			}
		}

	} catch (Exception e) {
		Echo("Exception: " + e);
	}
}
