local name = "serverclient";
local server = Env.Create(name); 
server.JSON = JSON or (loadfile "JSON.lua")();

assert(server.JSON, "JSON.lua missing");

local NETEVENT_CONNECTED = 1;
local NETEVENT_DISCONNECTED = 2;
local NETEVENT_SEND = 3;
local NETEVENT_RECEIVE = 4;

local events = {};
local buffer = "";
local length = nil;
local function Assemble()

	if buffer == "" then 
		return nil;
	end 

	if length == nil then 
		length = string.unpack("i4", buffer);
		buffer = buffer:sub(5);
		--print("Start:", length, "|"..buffer.."|");
	end
	
	if buffer:len() >= length then 
	
		local msg = buffer:sub(1, length);
		buffer = buffer:sub(length+1);
		length = nil;
		--print("Buffer:", "|"..buffer.."|");
		--print("Msg:", "|"..msg.."|");
		return msg;
	end 
	
	return nil;
end

events[NETEVENT_CONNECTED] = function(srv, ev)

	srv.Clients = {};
	srv.HalfOpen = true;
	srv.sessionid = nil;
	srv:ResetPing();
end

events[NETEVENT_DISCONNECTED] = function(srv, ev)

	srv.Clients = {};

	if srv.sessionid == nil or srv.HalfOpen == true then	
		return;
	end
	
	srv.DisconnectProc(srv.sessionid);
end

events[NETEVENT_SEND] = function(srv, ev)

end

events[NETEVENT_RECEIVE] = function(srv, ev)

	--[[print("RECV: ");
	for k,v in pairs(ev) do 
		print(k,v);
	end]]

	buffer = buffer .. ev.data;
end

function server:Connect(id, addr, port, recvproc, connectproc, disconnectproc)

	if self.socket then 
		self.socket:Disconnect();
		self.socket=nil;
	end 

	self.id = id;
	self.RecvProc = recvproc;
	self.ConnectProc = connectproc;
	self.DisconnectProc = disconnectproc;
	self.socket = Client.Connect(addr, port);
end

function server:Send(data)

	data = string.pack("i4", data:len())..data;

	self.socket:Send(data);
end

function server:SendMessageToTarget(target, parameter, data)

	if not self:GetConnectedServer(target) then 
		return false;
	end 

	data = data or {};
	parameter = parameter or "";
	
	local transmission = {SessionId=self.sessionid, TargetServerId=target, Action=2, Parameter=parameter, Data=data};
	
	self:Send(self.JSON:encode(transmission));
	
	self:ResetPing();

	return true;
end

function server:GetConnectedServer(serverid)

	local connected, err = self.socket:Status();

	if not connected then 
		return nil;
	elseif self.HalfOpen then
		return nil; 
	elseif not self.sessionid then 
		return nil;
	end

	serverid = serverid or self.id;

	return self.Clients[serverid];
end

server.PingTime = Timer.New();
server.PingTime:Start();

function server:ResetPing()
	self.PingTime = Timer.New();
	self.PingTime:Start();
end

function server:Ping()

	if self.PingTime:Elapsed() >= 5000 then 
	
		local transmission = {SessionId=self.sessionid, TargetServerId="00000000-0000-0000-0000-000000000000", Action=0, Parameter="ping", Data={}};
	
		self:Send(self.JSON:encode(transmission));
		self:ResetPing();
	end
end

function server:Tick()

	local event = self.socket:GetEvent();

	if event then 
		local func = events[event.type];
		if func then 
			func(self, event);
		end
	end	
	
	local msg = Assemble();
	if msg then 
		
		self:ResetPing();
		
		if self.HalfOpen then 
	
			if self.sessionid ~= nil then 
				self.HalfOpen = false;
				self.sessionid = msg;
				self.ConnectProc(msg);
			else
				self:Send(self.id);
				self.sessionid = msg;
			end
		else
			
			--print(msg);
			local ok, data = pcall(self.JSON.decode, self.JSON, msg);
		
			if not ok or type(data.Action) ~= "number" then 
				print(data);
			else
				
				if data.Action == 5 and data.Data then 
					self.Clients = data.Data;
				end

				self.RecvProc(data);
			end
		end
	else 
		self:Ping();
	end 
end

function server:NWServerStart(backendscript, serverid, address)

	local cli = self;

	function cli:LastGetAction()
	
		if self.LASTREQUEST and self.LASTREQUEST.Action then 
			return self.LASTREQUEST.Action;
		else
			return -1;
		end
	end 

	function cli:LastSender()
		
		if self.LASTREQUEST and self.LASTREQUEST.TargetServerId then 
			return self.LASTREQUEST.TargetServerId;
		else
			return "";
		end
	end 

	function cli:LastParameter()
		
		if self.LASTREQUEST and self.LASTREQUEST.Parameter then 
			return self.LASTREQUEST.Parameter;
		else
			return "";
		end
	end 
	
	function cli:GetData(key)
		
		if self.LASTREQUEST and self.LASTREQUEST.Data then 
			return self.LASTREQUEST.Data[tostring(key)] or "";
		else
			return "";
		end
	end 
		
	function cli:GetDataKey(first)
		
		if self.LASTREQUEST and self.LASTREQUEST.Data then 
			
			if first then		
				self.LASTREQUEST.Keys = {};
				self.LASTREQUEST.Next = 0;
				for k, v in pairs(self.LASTREQUEST.Data) do 
					table.insert(self.LASTREQUEST.Keys, k);
				end
			end
			
			self.LASTREQUEST.Next = self.LASTREQUEST.Next + 1;
			
			return self.LASTREQUEST.Keys[self.LASTREQUEST.Next] or "";
		else
			return "";
		end
	end 
		
	local function Recv(msg)

		if msg and msg.Parameter and (msg.Action == 2 or msg.Action == 5) then
			cli.LASTREQUEST = msg;
			NWN.RunScript(backendscript, 0);
		end

		print("Message: ");
	end

	local function Connect(msg)

		cli.LASTREQUEST = {Action=3, Parameter=msg, TargetServerId=self.id};
		NWN.RunScript(backendscript, 0);
		
		print("Session "..msg.." connected");
	end

	local function Disconnect(msg)

		cli.LASTREQUEST = {Action=4, Parameter=msg, TargetServerId=self.id};
		NWN.RunScript(backendscript, 0);
		
		print("Session "..msg.." disconnected");
	end

	local mainloop = Env.GetOrCreate("mainloop");

	self:Connect(serverid,address, 19836, Recv, Connect, Disconnect);
	
	mainloop.ClientProc = function()	
		cli:Tick();
	end

	NWN.HookMainloop(function(duration) 

		for k, v in pairs(mainloop) do 
			pcall(v, duration);
		end

		return false;
	end);
end

return server;