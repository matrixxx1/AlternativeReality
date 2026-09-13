# AI NPC mini quests and conversation history

## AI server change required

Keep the existing `npc_chats` project, chat upsert, request submission and polling API. Produce **one model response per player message**, containing both the NPC's dialogue and an optional `quest_request`. Do not make a second model call to select a location, reward or quest target.

Extend the schema your model generates **and** the validator/normalizer that builds `npc_result`. Preserve `quest_request` in the completed request's top-level `npc_result`; merely mentioning a quest in dialogue does not create one. Existing servers without this field continue to support ordinary chat.

Example completed response:

```json
{
  "guid": "the-request-guid",
  "status": "complete",
  "npc_result": {
    "dialogue": "My pockets have staged a rebellion! Could you help with {quest_title}? {objective} {location} I'll pay {reward}. You have {time_limit}.",
    "player_intent": "request_quest",
    "npc_intent": "offer_quest",
    "relationship_delta": 0.01,
    "end_conversation": false,
    "reason_code": "quest_offer",
    "memory_facts": [],
    "quest_request": {
      "requested": true,
      "title": "The Great Pocket Rebellion",
      "action": "find_item",
      "subject": "keepsake"
    }
  }
}
```

For ordinary conversation omit `quest_request`, use `null`, or return `{"requested":false}`. The AI decides whether the player's message asks for a task, job, favor, mission or quest; the game does not use keyword matching. Do not create quests in response to an ordinary greeting. Set `end_conversation:false` when offering a quest so the player can review it.

Suggested addition to the NPC system prompt:

> Determine whether the player is asking you for a mini quest. If so, invent a playful title of at most 80 characters and select one action from context_snapshot.quest_contract.supported_actions. Return quest_request alongside the normal dialogue fields in the same JSON result. Use the contract's placeholders for game-assigned facts. Do not choose coordinates, entity IDs, quantities, deadlines or rewards. The game may decline a proposal if no suitable target is available. Never claim that a proposed quest has been accepted or completed. Do not treat a previous quest proposal in memory as an active quest; use context_snapshot.quest_contract.active_quests.

## Supported actions

| Action | Game behavior |
| --- | --- |
| `find_pet` | Selects a nearby dog or cat. Player uses Capture pet and returns it to the giver. |
| `find_item` | Chooses a reachable search spot containing a lost keepsake. Player visits it, uses **Search marked spot** in the quest log, then returns the keepsake. |
| `collect` | Chooses a reachable material cache. Player searches it and brings back three materials. `subject` can suggest `wood`, `metal` or `kindling`; other hints default to wood. |
| `photograph` | Selects a nearby actor. Requires a camera and film (except God Mode). Player takes a new picture of that exact actor and returns the physical print. |
| `taunt` | Selects a nearby resident. Player uses the existing taunt action and returns. |
| `vandalize` | Selects a nearby unoccupied parked vehicle. Player damages it and returns; normal police consequences apply. |
| `hunt` | Selects a nearby rabbit, chicken or deer. Player defeats that specific animal and returns. |
| `chop` | Selects a nearby tree or bush. Requires a sword; player chops the marked object and returns. |

The game picks actual objects and locations within 120 meters with a directly traversable route or reachable approach. Search spots are 15–30 meters away. Unsupported actions and unavailable targets produce a local explanatory reply, with no additional AI request. Subject hints are not arbitrary item definitions or target names. This initial implementation uses a generic keepsake for `find_item`.

Rewards are currently $30, or $60 for hunting/vandalism, plus the existing quest-completion XP and relationship effects. The AI cannot override these. At most three AI quests may be active per player, with one active quest per giver.

## Dialogue placeholders

Use single braces and these exact names:

| Placeholder | Supplied by game |
| --- | --- |
| `{quest_title}` | Validated AI title |
| `{objective}` | Complete authoritative instructions |
| `{target_name}` | Selected actor, object or search item |
| `{item_name}` | Selected search item/material, or target label |
| `{quantity}` | Required quantity |
| `{location}` | Directions from the NPC to the selected target |
| `{reward}` | Formatted cash reward |
| `{time_limit}` | `10 minutes after accepting` |
| `{giver_name}` | NPC name |

The game expands the template before displaying and saving it. It also appends authoritative objective/reward instructions, so the quest remains understandable if the model omits placeholders. Do not emit other placeholders. The raw remote AI transcript may retain placeholders; the local game transcript stores the expanded text.

## Lifecycle and validation

1. The AI proposal becomes an offer with a **Review quest** button in chat.
2. The player accepts or declines using the existing quest window. Offers expire after ten minutes and do not survive a game-server restart.
3. Acceptance starts an exact ten-minute deadline. The player must finish the action **and return to hand it in** before that deadline.
4. Search results can be collected only once and only within four meters. Photo proof must show the selected actor, have been taken since acceptance and still be in the player's backpack; the print is consumed on hand-in.
5. The server pays only once. Both active and ready-but-unclaimed quests fail at the deadline. Deadlines persist in SQLite, so reconnecting or restarting does not reset them.
6. The quest log and map show the next objective, then the giver. The player's normal inventory, equipment and crime rules still apply. Unique keepsakes cannot be transferred or dropped and are removed on expiration/abandonment.

## Scrollable conversation history

History is isolated by reality, player character and stable AI NPC identity. Opening a chat loads the latest 50 messages, offers **Load older messages**, then sends the player's automatic `Hello!`. Saved messages include actual expanded quest replies and separate farewell lines. They survive reconnects and server restarts.

For conversations predating this update, the first empty local history is imported read-only from the configured AI server:

* `GET /api/v1/projects` (when the project GUID is not configured)
* `GET /api/v1/projects/{guid}/chats`, matching the exact `external_key`
* `GET /api/v1/chats/{guid}/messages`

These endpoints already work on the current AI server. The messages response contains `messages[]` with `guid`, `role` (`user`/`assistant`), `content`, `created_at`, and optional `npc_result.dialogue`. No model request is submitted to fetch history. The import has a ten-second timeout; if the remote service is unavailable, the UI explains that old remote messages could not be loaded and continues with locally saved messages. Replacing the NPC with a new identity starts a separate history.

## AI server checks

Test an ordinary greeting (no quest), a request for each supported action, a follow-up about an existing quest, and invalid/unavailable action output. Inspect the final serialized `npc_result` to ensure `quest_request` survives validation. Count inference jobs: each submitted player message must create only one model job, even when it offers a quest. The existing 300-second game timeout still applies to model responses.
