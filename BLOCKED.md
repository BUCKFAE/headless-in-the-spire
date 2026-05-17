# Blocked work

Engineering work that surfaced during the autonomous bug-hunting pass but
needs a human decision before it can land. Each entry names the surface,
the open question, and the cheapest unblocking step.

## SMITH rest-site option needs a follow-up card-pick wire surface

**Surface.** `run/select_rest_site_option(SMITH)` currently leaves the
player blocked on a card-select sub-flow that `Sts2Bindings.SelectRestSiteOption`
walks away from (`src/Sts2Headless.Runtime/Sts2Bindings.cs:1169-1170` —
"SMITH leaves the room pending a card-select; we leave that alone so a
future card-select wire can resume it"). The next snapshot reports
`CurrentRoomType=RestSiteRoom` with an empty `AvailableRestSiteOptions`
list and there is no wire call that can advance from that state.

**Why it's blocked.** The card-pick happens *between* two wire calls
(after `select_rest_site_option`, before the implicit "leave"), and the
right shape for it is a UX choice the agent has to make explicit. Three
plausible designs:

1. **Two calls** — `run/select_rest_site_option(SMITH)` returns a snapshot
   with a new `pendingCardSelection: { cards: [...], reason: "smith" }`
   field; a separate `run/rest_site_pick_card(cardIndex)` resolves it.
   - Pros: mirrors how rewards work (`available` list + `select_reward`).
   - Cons: introduces a new ambient "pending" state to every snapshot.
2. **Combined call** — `run/select_rest_site_option(SMITH, cardIndex)`
   takes the card index up-front; agent inspects the deck via a separate
   `run/state` query before deciding.
   - Pros: stateless, no new snapshot field, single round-trip.
   - Cons: caller has to predict the deck order, no preview of "is this
     card upgradable" without the snapshot saying so.
3. **Reuse `ICardSelector` bridge** — the `HeadlessCardSelector` shipped
   for Headbutt already queues caller-supplied card indices for in-combat
   prompts; the same queue plus a new `RunSelectRestSiteOptionParams.SmithCardIndex`
   field could feed it.
   - Pros: leverages existing bridge.
   - Cons: SMITH's engine path probably doesn't go through `CardSelectCmd`
     — needs investigation before this is viable.

Until one of the three is chosen, the Python and C# heuristic agents
both deliberately skip SMITH (`HeuristicAgent.DecideRestSite` prefers
HEAL → any non-SMITH → SMITH last; the Python
`HeuristicAgent.decide_rest_site` mirrors that exactly).

**Cheapest unblock.** Spend ~30 minutes probing the engine's SMITH path
(`RestSiteSynchronizer` + whatever handler the SMITH option calls)
to confirm whether it routes through `CardSelectCmd` or its own
synchronizer. That investigation rules in or out option 3 and makes the
decision between 1 and 2 a UX call.
