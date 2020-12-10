# Change Log

## v2.0.3
- Fixed TcpSettings setup...;

## v2.0.2
- Fixed TcpSettings setup;
- Fixed ConvertDataToUnknownType due to cant override function;

## v2.0.1 server
- Added TcpSettings;
- Added ForceIsClientConnected;

## v2.0.1 client
- Fixed client disconnection (not break while on ReceivePacket);
- Added TcpSettings;
- Fixed _Client.Connected on autoreconnect;

## v2.0.0
- Fixed headers separator;
- Fixed initializing connection after first unsuccess connect;
- Fixed header normalizing for exceptions;
- Fixed double client disconnection from server;
- Fixed client Stop();
- Rebuilded send/receive system for mass queue of sending;

## v1.0.0
- First version