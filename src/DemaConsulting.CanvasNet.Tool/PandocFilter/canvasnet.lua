-- canvasnet.lua: the Pandoc filter that draws models, charts, diagrams, and document pages with the canvasnet tool.
-- It needs DemaConsulting.CanvasNet.Tool 0.2.0-beta.11 or later: this shim starts "canvasnet pandoc-filter" once per
-- document, which does all the work. Written by "dotnet canvasnet pandoc-filter --write-shim <path>"; do not edit it,
-- write it again to update it. Reference: "dotnet canvasnet help pandoc". CANVASNET_TOOL replaces "dotnet canvasnet".
local PROTOCOL = "1"
local function fail(message) error("canvasnet.lua: " .. message, 0) end
local function warn(message)
  if pandoc.log and pandoc.log.warn then pandoc.log.warn(message)
  else io.stderr:write("[WARNING] " .. message .. "\n") end
end
-- CANVASNET_TOOL: white space separates, a double quote groups (including spaces), and there are no escapes.
local function split_command(text)
  local tokens, current, quoted, started = {}, {}, false, false
  local function finish()
    if #current == 0 then fail("CANVASNET_TOOL has an empty quoted part: " .. text) end
    table.insert(tokens, table.concat(current))
    current, started = {}, false
  end
  for char in text:gmatch(".") do
    if char == '"' then quoted, started = not quoted, true
    elseif not quoted and char:match("%s") then if started then finish() end
    else table.insert(current, char); started = true end
  end
  if quoted then fail("CANVASNET_TOOL has a quote that is never closed: " .. text) end
  if started then finish() end
  if #tokens == 0 then fail("CANVASNET_TOOL names no program") end
  return tokens
end
-- Only what the tool may draw needs it: a code block with one of CLASSES (exact), a code block with one of DRAW_CLASSES
-- and the class "draw" (without it the block stays code), or a local picture (no URL scheme) whose name ends in one of
-- SUFFIXES (any letter case). --write-shim fills the lists from the tool's own tables, so write the shim again after
-- updating the tool.
local CLASSES = @CLASSES@
local DRAW_CLASSES = @DRAW_CLASSES@
local SUFFIXES = @SUFFIXES@
local function has_suffix(name)
  for _, suffix in ipairs(SUFFIXES) do if name:sub(-#suffix) == suffix then return true end end
  return false
end
local function needs_tool(doc)
  local needed = false
  doc:walk({
    Image = function(img)
      if not img.src:match("^%a[%w+.-]+:") and has_suffix(img.src:lower()) then needed = true end
    end,
    CodeBlock = function(block)
      local opted = false
      for _, class in ipairs(block.classes) do if class == "draw" then opted = true end end
      for _, class in ipairs(block.classes) do
        for _, drawn in ipairs(CLASSES) do if class == drawn then needed = true end end
        if opted then
          for _, drawn in ipairs(DRAW_CLASSES) do if class == drawn then needed = true end end
        end
      end
    end,
  })
  return needed
end
-- Math is drawn only when the document's metadata sets canvasnet-math to true. Each Math node becomes inline code of the
-- class canvasnet-math that holds the MathML Pandoc's texmath writes for it (the attribute "tex" keeps the LaTeX and
-- "display" the kind), which the tool draws as a picture. Math texmath cannot convert stays as it is.
local function math_enabled(doc)
  local flag = doc.meta["canvasnet-math"]
  if flag == nil then return false end
  if type(flag) == "boolean" then return flag end
  return pandoc.utils.stringify(flag) == "true"
end
local function convert_math(doc)
  local converted = false
  local result = doc:walk({ Math = function(math)
    local display = math.mathtype == "DisplayMath"
    local para = pandoc.Plain({ math })
    local ok, html = pcall(pandoc.write, pandoc.Pandoc({ para }), "html", { html_math_method = "mathml" })
    local mathml = ok and html:match("<math.-</math>") or nil
    if not mathml then return nil end
    converted = true
    return pandoc.Code(mathml, pandoc.Attr("", { "canvasnet-math" },
      { { "tex", math.text }, { "display", display and "block" or "inline" } }))
  end })
  return result, converted
end
function Pandoc(doc)
  local converted = false
  if math_enabled(doc) then doc, converted = convert_math(doc) end
  if not converted and not needs_tool(doc) then return nil end
  local override = os.getenv("CANVASNET_TOOL")
  local command = (override and override:match("%S")) and split_command(override) or { "dotnet", "canvasnet" }
  local args = { table.unpack(command, 2) }
  local function add(...) for _, arg in ipairs({ ... }) do table.insert(args, arg) end end
  add("pandoc-filter", "--json", "--protocol", PROTOCOL, "--filter-path", PANDOC_SCRIPT_FILE, "--format", FORMAT,
    "--pandoc-version", tostring(PANDOC_VERSION))
  for _, dir in ipairs(PANDOC_STATE.resource_path or { "." }) do add("--resource-path", dir) end
  if PANDOC_STATE.output_file then add("--pandoc-output", PANDOC_STATE.output_file) end
  local has_embed, embed = pcall(function() return PANDOC_WRITER_OPTIONS.embed_resources end)
  if has_embed and embed then add("--embed-resources") end
  local ran, output = pcall(pandoc.pipe, command[1], args, pandoc.write(doc, "json"))
  if not ran then
    -- Only a table with a numeric error_code is a tool that ran and failed; anything else means it did not start
    local function field(name)
      local read, value = pcall(function() return output[name] end)
      if read then return value end
    end
    local code = type(output) == "table" and field("error_code") or nil
    if type(code) ~= "number" then
      local shown, reason = pcall(tostring, output)
      fail("cannot run the canvasnet tool ('" .. table.concat(command, " ") .. "'): "
        .. (shown and reason or "an error that cannot be shown")
        .. ". Pin DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json (dotnet tool install"
        .. " DemaConsulting.CanvasNet.Tool) or set CANVASNET_TOOL to the command to run.")
    end
    local printed = field("output")
    local decoded, result = pcall(pandoc.json.decode, type(printed) == "string" and printed or "", false)
    if decoded and type(result) == "table" and type(result.error) == "table" and result.error.message then
      fail(tostring(result.error.message) .. (result.error.hint and (" (" .. tostring(result.error.hint) .. ")") or ""))
    end
    fail("the canvasnet tool exited with code " .. tostring(code) .. " and printed no message")
  end
  local result = pandoc.read(output, "json")
  for _, message in ipairs(result.meta["canvasnet-warnings"] or {}) do
    warn("canvasnet.lua: " .. pandoc.utils.stringify(message))
  end
  result.meta["canvasnet-warnings"] = nil
  return result:walk({ Span = function(span)
    if span.classes[1] == "canvasnet-markdown" then
      return pandoc.utils.blocks_to_inlines(pandoc.read(span.attributes.markdown, "markdown").blocks)
    end
  end })
end
