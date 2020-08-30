string MyName = null;
IMyRadioAntenna antenna = null;
Dictionary<string, IMyBroadcastListener> Listeners = new Dictionary<string, IMyBroadcastListener>();
Dictionary<string, List<Action<MyIGCMessage>>> Handlers = new Dictionary<string, List<Action<MyIGCMessage>>>();

T GetFirstBlockOfType<T>() where T : class, IMyTerminalBlock
{
	var blocks = new List<T>();
	GridTerminalSystem.GetBlocksOfType(blocks);
	return blocks.Count > 0 ? blocks[0] : null;
}

string GetMyName()
{
	return IGC.Me.ToString();
}

void HandleIGCMessage(string channel, Action<MyIGCMessage> handler)
{
	IMyBroadcastListener Listener = IGC.RegisterBroadcastListener(channel); // may return one that already exists, that's okay
	Listener.SetMessageCallback(channel);
	Listeners.Add(channel, Listener);
	
	if (!Handlers.ContainsKey(channel))
	{
		Handlers[channel] = new List<Action<MyIGCMessage>>();
	}
	var channelHandlers = Handlers[channel];
	channelHandlers.Add(handler);
}

public Program()
{
	antenna = GetFirstBlockOfType<IMyRadioAntenna>();
	if (antenna == null) return;

	MyName = GetMyName();
	
	HandleIGCMessage("PING", (MyIGCMessage message) => {
		string SenderId = message.Data as string;
		Echo("Received PING from: " + SenderId);
		SendPong(SenderId);
	});

	HandleIGCMessage("PONG", (MyIGCMessage message) => {
		string SenderId = message.Data as string;
		Echo("Received PONG from: " + SenderId);
	});
	
	Echo("PING/PONG started for id: " + MyName);
}

public void SendPing()
{
	Echo("Sending PING");
	// SendBroadcastMessage(string, TData, TransmissionDistance)
	IGC.SendBroadcastMessage(
		"PING", // tag/channel
		MyName, // data
		TransmissionDistance.TransmissionDistanceMax // distance
	);
}

public void SendPong(string id)
{
	Echo("Sending PONG back to " + id);
	IGC.SendBroadcastMessage(
		"PONG", // tag/channel
		MyName, // data
		TransmissionDistance.TransmissionDistanceMax // distance
	);
}

public void Main(string argument, UpdateType updateSource)
{
	Echo("argument: " + argument + ", updateSource: " + updateSource);

	try {
		if (updateSource == UpdateType.Terminal || updateSource == UpdateType.Trigger)
		{
			SendPing();
		}
		else if (updateSource == UpdateType.IGC)
		{
			IMyBroadcastListener listener = null;
			List<Action<MyIGCMessage>> channelHandlers = null;
			if (Listeners.TryGetValue(argument, out listener) && Handlers.TryGetValue(argument, out channelHandlers))
			{
				while (listener.HasPendingMessage)
				{
					var message = listener.AcceptMessage();
					foreach (var handler in channelHandlers)
					{
						handler(message);
					}
				}
			}
		}

	} catch (Exception e) {
		Echo("Exception: " + e);
	}
}
