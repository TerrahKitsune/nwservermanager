#include "nwnx_backend"

void main(){

	int nAction = GetLastBackendActionType();
	string sParameter = GetLastBackendParameter();
	string sSender = GetLastBackendSender();
	
	if(nAction == SRV_ACTION_MESSAGE){
		SendMessageToPC(GetFirstPC(), sSender+" send a message with parameter "+sParameter);
		string key = GetFirstBackendDataKey();
		while(key != ""){
			SendMessageToPC(GetFirstPC(), key+": "+GetLastBackendData(key));
			key = GetNextBackendDataKey();
		}
			
		CreateMessageData();
		AddMessageData("Player", GetName(GetFirstPC()));
		SendMessageToServer(sSender, sParameter);
	}
	else if(nAction == SRV_ACTION_CLIENTSCONNECTEDUPDATE){
		SendMessageToPC(GetFirstPC(), "Backend clients connected update");
		string key = GetFirstBackendDataKey();
		while(key != ""){
			SendMessageToPC(GetFirstPC(), key+": "+GetLastBackendData(key));
			key = GetNextBackendDataKey();
		}
	}
	else if(nAction == SRV_ACTION_CONNECTED){
		SendMessageToPC(GetFirstPC(), sParameter+" connected to backend");
	}	
	else if(nAction == SRV_ACTION_DISCONNECTED){
		SendMessageToPC(GetFirstPC(), sParameter+" disconnected from backend");
	}	
}