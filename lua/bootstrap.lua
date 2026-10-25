local json = require("json")
local workshop = require("workshop").new()

function workshop_call(request)
  if #request > 1024 * 1024 then
    return json.encode({ok = false, error = "Request exceeds 1 MiB"})
  end
  local ok, result = pcall(function()
    return workshop:call(json.decode(request))
  end)
  if not ok then return json.encode({ok = false, error = tostring(result)}) end
  return json.encode(result)
end
