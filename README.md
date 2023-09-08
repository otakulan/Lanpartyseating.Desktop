# Lanpartyseating.Desktop

## Overview

Lanpartyseating.Desktop is a Microsoft Windows desktop client companion for the Lanparty-seating web application. This
desktop client, written in C#, aims to enhance the functionality of the web application by directly managing gaming
computers. Its primary purpose is to facilitate various actions related to gaming sessions, such as automatic login and
logout of users, session time extension or reduction without interrupting players, preparing computers for tournaments,
and monitoring computer status.

**Please Note:** As of the current release, none of the aforementioned functionality has been implemented, but
Lanpartyseating.Desktop is designed to evolve over time.

## Features

The core features planned for Lanpartyseating.Desktop include:

1. **Automatic User Login/Logout:** The client will automatically log users in at the start of gaming sessions and log
   them out at the end, simplifying the user experience and improving security.

2. **Session Time Management:** It will enable administrators to extend or shorten gaming sessions without disrupting
   players, ensuring smooth transitions between different activities during a LAN party.

3. **Tournament Preparation:** Lanpartyseating.Desktop will assist in preparing computers for tournaments, helping
   administrators manage and optimize the gaming environment.

4. **Computer Monitoring:** The client will provide real-time monitoring of computer status, allowing administrators to
   identify and address issues promptly.

### Phoenix.Channel

The client connects to a Phoenix.Channel within the web application, enabling seamless communication between the desktop
client and the server. The Phoenix.Channel is a WebSocket-based transport that allows for realtime bidirectional data
transfer.

### Broadcast Messages

Lanpartyseating.Desktop operates by reacting to broadcast messages received from the server. These messages are sent by
the server to all connected clients and are used to trigger various actions within the client.

## License

Lanpartyseating.Desktop is released under the [MIT License](https://opensource.org/licenses/MIT).