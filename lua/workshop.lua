-- Authoring state and execution share object IDs, property validation, and graph nodes.
local M = {}
local json = require("json")
local MAX_OBJECTS, MAX_NODES, MAX_LINKS, MAX_HISTORY = 128, 128, 256, 48
local kinds = {panel=true, button=true, label=true, orb=true, cube=true}
local types = {event=true, set=true, toggle=true, animate=true, script=true}
local defaults = {label="Object", x=0, y=0, z=0, width=180, height=70, depth=1,
  rotation=0, color={0.42,0.72,1,1}, visible=true, territory=0}
local number_bounds = {x={-100000,100000}, y={-100000,100000}, z={-100000,100000},
  width={0.01,10000}, height={0.01,10000}, depth={0.01,10000},
  rotation={-100000,100000}, territory={0,65535}}
local animatable = {x=true, y=true, z=true, width=true, height=true, depth=true, rotation=true}

local function copy(v)
  if type(v) ~= "table" then return v end
  local out = {}
  for k, value in pairs(v) do out[k] = copy(value) end
  return out
end
local function equal(a,b)
  if type(a) ~= type(b) then return false end
  if type(a) ~= "table" then return a == b end
  for k,v in pairs(a) do if not equal(v,b[k]) then return false end end
  for k in pairs(b) do if a[k] == nil then return false end end
  return true
end
local function text(v, name, max)
  assert(type(v)=="string" and #v<=(max or 128), (name or "Text") .. " is too long or is not text")
  return v
end
local function finite(v, lo, hi)
  assert(type(v)=="number" and v==v and v>=lo and v<=hi, "Number is outside its allowed range")
  return v
end
local function identifier(v)
  text(v,"ID",48)
  assert(v:match("^[%a_][%w_-]*$"), "IDs must start with a letter or underscore")
  return v
end
local function array(v, max)
  assert(type(v)=="table" and #v<=max, "List exceeds its allowed size")
  local count=0
  for k in pairs(v) do
    assert(type(k)=="number" and k%1==0 and k>=1 and k<=#v, "Expected a dense list")
    count=count+1
  end
  assert(count==#v,"Expected a dense list")
  return v
end
local function property(key,value)
  if number_bounds[key] then
    finite(value, table.unpack(number_bounds[key]))
    if key=="territory" then assert(value%1==0,"Territory must be an integer") end
  elseif key=="color" then
    assert(#array(value,4)==4,"Color needs RGBA")
    for _,v in ipairs(value) do finite(v,0,1) end
  elseif key=="visible" then assert(type(value)=="boolean","Visible must be a boolean")
  elseif key=="label" then text(value,"Label",256)
  else error("Unknown property: " .. tostring(key)) end
  return copy(value)
end
local function find(list,id)
  for _,v in ipairs(list) do if v.id==id then return v end end
  error("Unknown ID: " .. tostring(id))
end
local function compile(code)
  text(code,"Script",16384)
  local chunk, err = load(code,"script","t",{})
  assert(chunk, err)
end

local function validate(input)
  assert(type(input)=="table" and input.version==1,"Expected a version 1 scene")
  local doc={version=1,name=text(input.name,"Scene name"),objects={},nodes={},links={}}
  local objects,nodes={},{ }
  for _,value in ipairs(array(input.objects,MAX_OBJECTS)) do
    assert(type(value)=="table","Invalid object")
    local object={id=identifier(value.id),kind=value.kind}
    assert(kinds[object.kind],"Unknown object kind")
    assert(not objects[object.id],"Duplicate object ID")
    for key,default in pairs(defaults) do
      local v=value[key]; if v==nil then v=default end
      object[key]=property(key,v)
    end
    if value.kind=="orb" or value.kind=="cube" then
      if value.width==nil then object.width=1 end
      if value.height==nil then object.height=1 end
    end
    objects[object.id]=object
    doc.objects[#doc.objects+1]=object
  end
  for _,v in ipairs(array(input.nodes,MAX_NODES)) do
    assert(type(v)=="table","Invalid node")
    local n={id=identifier(v.id),type=v.type,object=v.object,
      x=finite(v.x or 30,-10000,10000),y=finite(v.y or 30,-10000,10000)}
    assert(types[n.type],"Unknown node type")
    assert(objects[n.object],"Node refers to a missing object")
    assert(not nodes[n.id],"Duplicate node ID")
    if n.type=="event" then
      assert(v.event=="click" or v.event=="tick" or v.event=="start","Unknown event")
      n.event=v.event
    elseif n.type=="toggle" then
      n.property="visible"
    elseif n.type=="set" then
      n.property=v.property; n.value=property(n.property,v.value)
    elseif n.type=="animate" then
      assert(animatable[v.property],"This property cannot animate")
      n.property=v.property; n.value=property(n.property,v.value)
      n.duration=finite(v.duration or 1,0.01,120)
    elseif n.type=="script" then
      compile(v.code); n.code=v.code
    end
    nodes[n.id]=n
    doc.nodes[#doc.nodes+1]=n
  end
  local edges,seen={},{}
  for _,v in ipairs(array(input.links,MAX_LINKS)) do
    assert(type(v)=="table" and nodes[v.from] and nodes[v.to],"Link refers to a missing node")
    assert(nodes[v.to].type~="event","Event nodes cannot have incoming wires")
    local key=v.from .. "/" .. v.to
    assert(not seen[key],"Duplicate wire"); seen[key]=true
    edges[v.from]=edges[v.from] or {}; table.insert(edges[v.from],v.to)
    table.insert(doc.links,{from=v.from,to=v.to})
  end
  local active,visited={},{}
  local function visit(id)
    assert(not active[id],"Graph cycles are not allowed; use a tick event")
    if visited[id] then return end
    active[id]=true
    for _,target in ipairs(edges[id] or {}) do visit(target) end
    active[id]=nil; visited[id]=true
  end
  for id in pairs(nodes) do visit(id) end
  assert(#json.encode(doc)<=512*1024,"Scene exceeds 512 KiB")
  return doc
end

local Engine={}; Engine.__index=Engine
function Engine:log(message)
  table.insert(self.logs,tostring(message):sub(1,1024))
  if #self.logs>32 then table.remove(self.logs,1) end
end
function Engine:reset()
  self.live=copy(self.doc.objects); self.animations={}; self.time=0; self.disabled={}
end
function Engine:commit(doc, group, preserve_runtime)
  doc=validate(doc)
  if equal(doc,self.doc) then return end
  if not group or group~=self.group then
    table.insert(self.undo,copy(self.doc))
    if #self.undo>MAX_HISTORY then table.remove(self.undo,1) end
  end
  self.group=group; self.redo={}; self.doc=doc; self.revision=self.revision+1
  if not preserve_runtime then self:reset() end
  if self.selected~="" then
    local found=false
    for _,v in ipairs(doc.objects) do if v.id==self.selected then found=true end end
    if not found then self.selected="" end
  end
end
local function set_live(tx,id,key,value)
  find(tx.objects,id)[key]=property(key,value)
  -- Setting an animated property takes ownership from its previous animation.
  for i=#tx.animations,1,-1 do
    local a=tx.animations[i]
    if a.object==id and a.property==key then table.remove(tx.animations,i) end
  end
end
local function animate(tx,id,key,target,duration,time)
  assert(animatable[key],"This property cannot animate")
  property(key,target); finite(duration,0.01,120)
  local start=find(tx.objects,id)[key]
  set_live(tx,id,key,start)
  table.insert(tx.animations,{object=id,property=key,from=start,to=target,start=time,duration=duration})
end
local safe_math={}
for _,k in ipairs({"abs","acos","asin","atan","ceil","cos","deg","exp","floor","fmod","huge","log","max","min","modf","pi","rad","sin","sqrt","tan","tointeger","type","ult"}) do safe_math[k]=math[k] end
local function object_handle(tx,id,time)
  find(tx.objects,id)
  return {
    get=function(_,key) assert(defaults[key]~=nil,"Unknown property"); return copy(find(tx.objects,id)[key]) end,
    set=function(_,key,value) set_live(tx,id,key,value) end,
    animate=function(_,key,value,duration) animate(tx,id,key,value,duration or 1,time) end,
  }
end
function Engine:run_script(node, tx, event, dt, budget)
  local env={assert=assert,error=error,ipairs=ipairs,pairs=pairs,next=next,select=select,
    tonumber=tonumber,tostring=tostring,type=type,math=copy(safe_math),
    event=event,time=self.time,dt=dt,self=object_handle(tx,node.object,self.time),
    scene={get=function(_,id) return object_handle(tx,id,self.time) end},
    log=function(message) table.insert(tx.logs,tostring(message):sub(1,1024)) end}
  local f,err=load(node.code,"node:" .. node.id,"t",env)
  assert(f,err)
  debug.sethook(function()
    budget.left=budget.left-1000
    if budget.left<=0 then error("Script instruction budget exceeded",0) end
  end,"",1000)
  local ok,message=pcall(f)
  debug.sethook()
  assert(ok,message)
end
function Engine:dispatch(source,dt,budget)
  if self.disabled[source.id] then return end
  local tx={objects=copy(self.live),animations=copy(self.animations),logs={}}
  local byid,edges={},{}
  for _,n in ipairs(self.doc.nodes) do byid[n.id]=n end
  for _,link in ipairs(self.doc.links) do
    edges[link.from]=edges[link.from] or {}; table.insert(edges[link.from],link.to)
  end
  local visited={}
  local function run(id)
    if visited[id] then return end
    visited[id]=true
    local n=byid[id]
    if n.type=="set" then set_live(tx,n.object,n.property,n.value)
    elseif n.type=="toggle" then set_live(tx,n.object,"visible",not find(tx.objects,n.object).visible)
    elseif n.type=="animate" then animate(tx,n.object,n.property,n.value,n.duration,self.time)
    elseif n.type=="script" then self:run_script(n,tx,source.event,dt,budget) end
    for _,target in ipairs(edges[id] or {}) do run(target) end
  end
  local ok,err=pcall(run,source.id)
  debug.sethook()
  if ok then
    self.live=tx.objects; self.animations=tx.animations
    for _,message in ipairs(tx.logs) do self:log(message) end
  else
    self:log(source.id .. ": " .. tostring(err))
    self.disabled[source.id]=true
  end
end
function Engine:event(kind,id,dt)
  local budget={left=50000}
  for _,n in ipairs(self.doc.nodes) do
    if n.type=="event" and n.event==kind and (not id or n.object==id) then
      self:dispatch(n,dt or 0,budget)
    end
  end
end
function Engine:tick(dt)
  finite(dt,0,10)
  if not self.playing then return end
  dt=math.min(dt,0.1); self.time=self.time+dt
  for i=#self.animations,1,-1 do
    local a=self.animations[i]
    local t=math.min(1,(self.time-a.start)/a.duration)
    find(self.live,a.object)[a.property]=a.from+(a.to-a.from)*t
    if t>=1 then table.remove(self.animations,i) end
  end
  self:event("tick",nil,dt)
end
function Engine:snapshot(full)
  local out={ok=true,objects=copy(self.live),playing=self.playing,time=self.time,
    selected=self.selected,revision=self.revision,dirty=not equal(self.saved,self.doc),
    canUndo=#self.undo>0,canRedo=#self.redo>0,logs=copy(self.logs)}
  if full then out.scene=copy(self.doc) end
  return out
end
local function new_id(prefix,list)
  local used={}; for _,v in ipairs(list) do used[v.id]=true end
  local i=1; while used[prefix .. i] do i=i+1 end
  return prefix .. i
end
function Engine:call(r)
  assert(type(r)=="table" and type(r.op)=="string","Expected a command")
  local op=r.op
  if op=="snapshot" then return self:snapshot(true)
  elseif op=="tick" then self:tick(r.dt); return self:snapshot(false)
  elseif op=="select" then
    if r.id~="" then find(self.doc.objects,r.id) end
    self.selected=r.id; self.group=nil
  elseif op=="play" then
    self.playing=not not r.enabled; self.group=nil; self:reset()
    if self.playing then self:event("start") end
  elseif op=="click" then
    find(self.doc.objects,r.id)
    if self.playing then self:event("click",r.id) else self.selected=r.id end
  elseif op=="edit" then
    local doc=copy(self.doc); local o=find(doc.objects,r.id)
    assert(type(r.patch)=="table","Expected properties")
    for key,value in pairs(r.patch) do o[key]=property(key,value) end
    self:commit(doc,r.group)
  elseif op=="add" then
    assert(kinds[r.kind],"Unknown object kind")
    local doc=copy(self.doc); local id=new_id(r.kind,doc.objects)
    local o={id=id,kind=r.kind,label=r.kind .. " " .. id,x=100,y=140}
    if r.kind=="orb" or r.kind=="cube" then
      o.x=0; o.y=0; o.width=1; o.height=1
    end
    for key,value in pairs(r.patch or {}) do o[key]=property(key,value) end
    table.insert(doc.objects,o); self:commit(doc); self.selected=id
  elseif op=="duplicate" then
    local doc=copy(self.doc); local o=copy(find(doc.objects,r.id))
    o.id=new_id(o.kind,doc.objects); o.label=o.label .. " copy"
    o.x=o.x+((o.kind=="orb" or o.kind=="cube") and 1 or 24)
    table.insert(doc.objects,o); self:commit(doc); self.selected=o.id
  elseif op=="delete" then
    find(self.doc.objects,r.id)
    local doc=copy(self.doc); local removed={}
    for i=#doc.objects,1,-1 do if doc.objects[i].id==r.id then table.remove(doc.objects,i) end end
    for i=#doc.nodes,1,-1 do
      if doc.nodes[i].object==r.id then removed[doc.nodes[i].id]=true; table.remove(doc.nodes,i) end
    end
    for i=#doc.links,1,-1 do
      if removed[doc.links[i].from] or removed[doc.links[i].to] then table.remove(doc.links,i) end
    end
    self:commit(doc)
  elseif op=="node" then
    local doc=copy(self.doc); local node=copy(r.node)
    assert(type(node)=="table","Expected a node")
    local layout_only=false
    if not node.id or node.id=="" then
      node.id=new_id("node",doc.nodes); table.insert(doc.nodes,node)
    else
      local existing=find(doc.nodes,node.id)
      local previous=copy(existing)
      for k in pairs(existing) do existing[k]=nil end
      for k,v in pairs(node) do existing[k]=v end
      -- Normalize host DTO defaults before deciding whether only layout changed.
      doc=validate(doc)
      local compare=copy(find(doc.nodes,node.id)); compare.x=previous.x; compare.y=previous.y
      layout_only=equal(compare,previous)
    end
    self:commit(doc,r.group,layout_only)
  elseif op=="delete_node" then
    local doc=copy(self.doc); find(doc.nodes,r.id)
    for i=#doc.nodes,1,-1 do if doc.nodes[i].id==r.id then table.remove(doc.nodes,i) end end
    for i=#doc.links,1,-1 do
      if doc.links[i].from==r.id or doc.links[i].to==r.id then table.remove(doc.links,i) end
    end
    self:commit(doc)
  elseif op=="link" or op=="unlink" then
    local doc=copy(self.doc)
    if op=="link" then table.insert(doc.links,{from=r.from,to=r.to})
    else
      for i=#doc.links,1,-1 do
        if doc.links[i].from==r.from and doc.links[i].to==r.to then table.remove(doc.links,i) end
      end
    end
    self:commit(doc)
  elseif op=="undo" or op=="redo" then
    local source=op=="undo" and self.undo or self.redo
    local dest=op=="undo" and self.redo or self.undo
    if #source>0 then
      table.insert(dest,copy(self.doc)); self.doc=table.remove(source)
      self.group=nil; self.selected=""; self.revision=self.revision+1; self:reset()
    end
  elseif op=="load" then
    local doc=validate(r.scene)
    self:commit(doc); self.saved=copy(self.doc); self.selected=""
  elseif op=="export" then return {ok=true,scene=copy(self.doc)}
  elseif op=="saved" then
    -- Acknowledge only the exact revision that was written by the host.
    if r.revision==self.revision then self.saved=copy(self.doc) end
  elseif op=="clear_logs" then self.logs={}
  else error("Unknown command: " .. op) end
  return self:snapshot(true)
end
function M.new()
  local doc={version=1,name="Untitled workshop",objects={},nodes={},links={}}
  local self=setmetatable({doc=doc,saved=copy(doc),undo={},redo={},logs={},revision=0,
    selected="",playing=false},Engine)
  self:reset()
  return self
end
return M
