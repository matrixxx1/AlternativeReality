# AlternativeReality AI server notes

This testing checkpoint adds optional LocalAI-backed conversations for special game NPCs. The game remains authoritative: LocalAI can return dialogue, classified intent, a small friend/foe adjustment, and a request to end the conversation, but it cannot run tools, modify files, issue game commands, or directly change world state.

## LocalAI server configuration

The committed default is Matt's Mac LocalAI server:

```json
"LocalAI": {
  "Enabled": true,
  "BaseUrl": "http://mmac.local:80",
  "ProjectGuid": "",
  "ProjectName": "AlternativeReality",
  "RequestTimeoutSeconds": 60
}
```

These values are in `src/AlternateEarth.Server/appsettings.json`. Change `BaseUrl` to select another LocalAI machine. ASP.NET configuration also permits machine-local environment overrides, which avoids changing a tracked file. For example, in Windows PowerShell:

```powershell
$env:LocalAI__BaseUrl = "http://<mac-tailscale-address>:80"
$env:LocalAI__ProjectName = "AlternativeReality"
dotnet run --project src/AlternateEarth.Server
```

If port 80 is unavailable on that machine, use the LocalAI port configured for that machine, such as `http://<mac-tailscale-address>:8080`. Set `LocalAI__ProjectGuid` to a specific project GUID to skip name lookup. Set `LocalAI__Enabled=false` to deliberately test offline behavior.

The LocalAI checkout must include the `NPC Chats` project type and Project API version 1.3. Create one project with:

- Name: `AlternativeReality`
- Type: `NPC Chats`
- A text-capable chat model and connection

When `ProjectGuid` is blank, AlternativeReality finds that project by its exact name and type. Each NPC/player pair is automatically upserted as one durable chat, so their private conversation history can be reviewed in LocalAI and recalled later.

## Run and test on Windows

From an up-to-date AlternativeReality checkout:

```powershell
dotnet restore AlternateEarth.sln
dotnet test AlternateEarth.sln
dotnet run --project src/AlternateEarth.Server
```

Then open `http://localhost:5080`.

1. Sign in, create/select a character, and make sure the account has a Home.
2. Find the ordinary NPC near the Home whose displayed name begins with `*`.
3. Open Actions on that NPC and choose **Talk**. The private conversation uses chat bubbles.
4. Tell the NPC a distinctive fact such as `the secret codeword is moo`, close the conversation, then speak with the same NPC/player pair again and ask for it.
5. Open LocalAI and verify the `AlternativeReality` project contains a chat named for that NPC and player. Review the turn and its game context there.
6. Insult or threaten the NPC and confirm the friend/foe value can move downward only by a small bounded amount per turn. At `-5`, the NPC says `Buzz off.` and ends the conversation.
7. Say goodbye and confirm the conversation ends when LocalAI classifies that intent.
8. Stop LocalAI or set `LocalAI__Enabled=false`. A new attempt must return exactly `Sorry, I can't talk now` and end cleanly instead of blocking gameplay.
9. Attack the AI NPC. Dialogue must end, and the NPC must otherwise fight, flee, take damage, kill, or be killed under the same existing rules as an ordinary NPC.
10. Kill the Home AI NPC. No replacement should appear immediately. After five real minutes, a replacement should appear near that Home if no AI NPC is present.

Also test with two player accounts. Each player must get a separate LocalAI chat and history even when speaking to the same NPC.

## Context sent to LocalAI

Each turn includes a fresh, server-built snapshot of game/reality identity, game time and weather, location and terrain, nearby buildings, NPCs, animals and players, the player and NPC stats, equipment and carried inventory, hunger/thirst/stamina/health, testing-cheat settings, and the current friend/foe rating. Player text is treated only as dialogue; it cannot override the sandbox or supply trusted game state.

## Diagnostics

- `GET /api/status` reports whether LocalAI is configured and which base URL the game server is using.
- AlternativeReality server logs warn when the LocalAI project cannot be found or a request fails.
- A missing server, timeout, bad response, missing project, or failed model request all use the safe offline response.

This checkpoint was compiled on the Mac with an isolated .NET 8 SDK, and the focused browser/contract tests passed. GitHub CI repeats the full build and test suite; the Windows commands above remain the final platform and live-game verification.
