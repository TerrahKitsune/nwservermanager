local _exit=Exit;Exit=function(ret) GetKey(); return ret+1; end

local cli = dofile("C:\\Users\\Terrah\\source\\repos\\nwservermanager\\nwservermanager\\nwscript\\nwserverbackend.lua");

local function printtbl(tbl)
	
	for k,v in pairs(tbl) do 
		print(k, v);
		if type(v) == "table" then 
			printtbl(v);
		end 
	end
end

local function Recv(msg)

	print("Message: ");
	printtbl(msg);

	if msg.Action == 6 then 
		print("Web request from: "..msg.Data.IP);
		cli:SendMessageToTarget(msg.TargetServerId, msg.Parameter .." -> ".. msg.Data.BODY, msg.Data);
	end
end

local function Connect(msg)

	print("Session "..msg.." connected");
end

local function Disconnect(msg)

	print("Session "..msg.." disconnected");
end

cli:Connect("14f3a874-224b-49fc-9437-37452926d37e","localhost", 19836, Recv, Connect, Disconnect);

while true do 

	cli:Tick();
	
	if HasKeyDown() then
		local key = string.char(GetKey());
		if not cli:SendMessageToTarget("14f3a874-224b-49fc-9437-37452926d37f", "Press", {Key=key}) then 
			print("Message not delivered");
		end
	end
	
	Sleep(1);
end 