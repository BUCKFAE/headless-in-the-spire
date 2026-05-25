# jq filter for Claude Code's `--output-format stream-json` events.
# Used by `just play-claude` to render one readable line per event
# instead of waterfalling raw JSON-lines to the terminal.
#
# Event shape (Claude Code SDK): each line is a JSON object with a `.type`
# of "system" (startup), "assistant" (model reply), "user" (tool result
# echo), or "result" (final summary). Assistant/user messages carry an
# Anthropic Messages-API `.message.content[]` array of `text` / `tool_use`
# / `tool_result` blocks.

if .type == "system" then
  "[init] model=" + (.model // "?")
elif .type == "assistant" then
  (.message.content[]? |
    if .type == "text" then
      "[claude] " + .text
    elif .type == "tool_use" then
      "[call] " + .name + " " + (.input | tojson)
    else
      empty
    end)
elif .type == "user" then
  (.message.content[]? |
    if .type == "tool_result" then
      "[result] " + (
        .content
        | if type == "string" then .
          elif type == "array" then (map(.text? // tojson) | join(" "))
          else tojson
          end
      )
    else
      empty
    end)
elif .type == "result" then
  "[done] " + (.subtype // "ok") +
  (if .total_cost_usd then " cost=$" + (.total_cost_usd | tostring) else "" end)
else
  empty
end
