#include "nwnx_lua"

const int SRV_ACTION_MESSAGE = 2;
const int SRV_ACTION_CONNECTED = 3;
const int SRV_ACTION_DISCONNECTED = 4;
const int SRV_ACTION_CLIENTSCONNECTEDUPDATE = 5;

//Start the backend client, this should only be done once per module start
//serverId must be a GUID thats authenticated in the backend
//backendAddress is the address to the backed
//backendNWScript is the nwscript thats going to be the eventscript for the backend
void StartBackendCliet(string serverId, string backendAddress, string backendNWScript);

//Retrives what kind of event is being triggred
//Returns SRV_ACTION_* constant
int GetLastBackendActionType();

//Get the last backend parameter that was provided
//For CONNECTED and DISCONNECTED this will be the *session* id
string GetLastBackendParameter();

//Get the sender that triggered the backend event
//For CONNECTED and DISCONNECTED this will be the *server* id
string GetLastBackendSender();

//Retrive a value from the last event, returns "" if nothing exists with the provided key
string GetLastBackendData(string sKey);

//Retrive the first key available in an incoming request
string GetFirstBackendDataKey();

//Retrive the first key available in an incoming request
string GetNextBackendDataKey();

//Send a message with the parameter to the target server
//Use CreateMessageData and AddMessageData to add additional data
void SendMessageToServer(string sServerId, string sParameter);

//Creates a data object that contains additional data to be sent in a SendMessageToServer call
//use AddMessageData to add data to the object
void CreateMessageData();

//Adds data to the additional data to be sent
void AddMessageData(string sKey, string sValue);

string GetFirstBackendDataKey(){

	return RunLuaString("return Env.Get('serverclient'):GetDataKey(true);");
}

string GetNextBackendDataKey(){

	return RunLuaString("return Env.Get('serverclient'):GetDataKey(false);");
}

void AddMessageData(string sKey, string sValue){

	RunLuaString("local e=Env.Get('serverclient'); e.DATA['"+sKey+"']=[=["+sValue+"]=];");
}

void CreateMessageData(){

	RunLuaString("local e=Env.Get('serverclient'); e.DATA={};");
}

void SendMessageToServer(string sServerId, string sParameter)
{
	RunLuaString("local e=Env.Get('serverclient'); e:SendMessageToTarget('"+sServerId+"', '"+sParameter+"', e.DATA); e.DATA=nil;");	
}

string GetLastBackendData(string sKey){

	return RunLuaString("return Env.Get('serverclient'):GetData([=["+sKey+"]=]);");
}

string GetLastBackendSender(){
	return RunLuaString("return Env.Get('serverclient'):LastSender();");
}

string GetLastBackendParameter(){

	return RunLuaString("return Env.Get('serverclient'):LastParameter();");
}

int GetLastBackendActionType(){

	return StringToInt(RunLuaString("return Env.Get('serverclient'):LastGetAction();"));
}

void StartBackendCliet(string serverId, string backendAddress, string backendNWScript){

	RunLuaString("local backend=dofile('nwserverbackend.lua'); backend:NWServerStart('"+backendNWScript+"', '"+serverId+"', '"+backendAddress+"');");
}