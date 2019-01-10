#include "nwnx_backend"

//Example script
void main(){

	//This is the type of event that fired the script
	int nAction = GetLastBackendActionType();
	//This will be what the remote side sParameter in a SendMessageToServer
	string sParameter = GetLastBackendParameter();
	//This is the serverid of the remote side that is sending to you
	string sSender = GetLastBackendSender();
	
	//Example we received a message
	if(nAction == SRV_ACTION_MESSAGE){
		SendMessageToPC(GetFirstPC(), sSender+" send a message with parameter "+sParameter);
		
		//Iterate the keys this is what the remoteside sent with CreateMessageData and AddMessageData
		string key = GetFirstBackendDataKey();
		while(key != ""){
			SendMessageToPC(GetFirstPC(), key+": "+GetLastBackendData(key));
			key = GetNextBackendDataKey();
		}
		
		//For a test respond with the player name under the key "Player"
		CreateMessageData();
		AddMessageData("Player", GetName(GetFirstPC()));
		SendMessageToServer(sSender, sParameter);
	}
	//This is a meta event, it'll fire every time a client disconnects or connects
	//the additional data here will contains which servers are currently connected and their names
	else if(nAction == SRV_ACTION_CLIENTSCONNECTEDUPDATE){
		SendMessageToPC(GetFirstPC(), "Backend clients connected update");
		string key = GetFirstBackendDataKey();
		while(key != ""){
			SendMessageToPC(GetFirstPC(), key+": "+GetLastBackendData(key));
			key = GetNextBackendDataKey();
		}
	}
	//This fires when *this client* connects to the backend
	else if(nAction == SRV_ACTION_CONNECTED){
		SendMessageToPC(GetFirstPC(), sParameter+" connected to backend");
	}	
	//This fires when *this client* disconnects to the backend
	else if(nAction == SRV_ACTION_DISCONNECTED){
		SendMessageToPC(GetFirstPC(), sParameter+" disconnected from backend");
	}	
}