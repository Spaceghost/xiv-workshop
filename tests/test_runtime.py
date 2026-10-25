"""Exercise the actual Nelua ABI and Lua VM, without FFXIV or a mocked runtime."""
import copy
import ctypes as c
import json
import math
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
LIB = c.CDLL(str(ROOT / "build/native/libxiv_workshop.so"))
LIB.ws_create.argtypes = [c.c_char_p]
LIB.ws_create.restype = c.c_void_p
LIB.ws_call.argtypes = [c.c_void_p, c.c_char_p]
LIB.ws_call.restype = c.c_char_p
LIB.ws_error.argtypes = [c.c_void_p]
LIB.ws_error.restype = c.c_char_p
LIB.ws_destroy.argtypes = [c.c_void_p]
LIB.ws_memory.argtypes = [c.c_void_p]
LIB.ws_memory.restype = c.c_size_t
LIB.ws_box_vertex.argtypes = [c.c_int] + [c.c_float] * 7 + [c.POINTER(c.c_float)] * 3
BUNDLE = (ROOT / "build/plugin/lua/workshop.lua").read_bytes()


class Runtime:
    def __init__(self):
        self.handle = LIB.ws_create(BUNDLE)
        if not self.handle or LIB.ws_error(self.handle):
            raise RuntimeError(LIB.ws_error(self.handle))

    def close(self):
        if self.handle:
            LIB.ws_destroy(self.handle)
            self.handle = None

    def call(self, op, **args):
        raw = LIB.ws_call(self.handle, json.dumps(dict(op=op, **args)).encode())
        if raw is None:
            raise RuntimeError(LIB.ws_error(self.handle))
        return json.loads(raw)


def scene():
    return dict(version=1, name="Test workshop", objects=[
        dict(id="button", kind="button", label="Change orb", x=40, y=80),
        dict(id="orb", kind="orb", label="Orb", x=1, y=2, z=3),
    ], nodes=[
        dict(id="click", type="event", object="button", event="click"),
        dict(id="color", type="set", object="orb", property="color", value=[1, .3, .5, 1]),
        dict(id="script", type="script", object="orb", code='self:animate("y", 4, 1); log("glow")'),
    ], links=[dict(from_="click", to="color"), dict(from_="color", to="script")])


def fixture():
    doc = scene()
    for link in doc["links"]:
        link["from"] = link.pop("from_")
    return doc


class RuntimeTests(unittest.TestCase):
    def setUp(self):
        self.vm = Runtime()
        self.addCleanup(self.vm.close)
        self.assertTrue(self.vm.call("load", scene=fixture())["ok"])

    def ok(self, op, **args):
        result = self.vm.call(op, **args)
        self.assertTrue(result["ok"], result)
        return result

    def obj(self, state, id="orb"):
        return next(o for o in state["objects"] if o["id"] == id)

    def test_visual_actions_and_script_share_objects(self):
        self.ok("play", enabled=True)
        state = self.ok("click", id="button")
        self.assertEqual([1, .3, .5, 1], self.obj(state)["color"])
        self.assertEqual(["glow"], state["logs"])
        for _ in range(10):
            state = self.ok("tick", dt=.1)
        self.assertAlmostEqual(4, self.obj(state)["y"])
        self.assertEqual(2, self.ok("export")["scene"]["objects"][1]["y"])
        self.assertEqual(2, self.obj(self.ok("play", enabled=False))["y"])

    def test_undo_groups_drag_redo_and_new_branch(self):
        self.ok("edit", id="orb", patch=dict(x=2), group="drag1")
        self.ok("edit", id="orb", patch=dict(x=3), group="drag1")
        self.assertEqual(1, self.obj(self.ok("undo"))["x"])
        self.assertEqual(3, self.obj(self.ok("redo"))["x"])
        self.ok("undo")
        state = self.ok("edit", id="orb", patch=dict(x=5))
        self.assertFalse(state["canRedo"])

    def test_failed_event_rolls_back_graph_and_script(self):
        doc = fixture()
        doc["nodes"][2]["code"] = 'self:set("x", 8); log("not committed"); error("broken")'
        self.ok("load", scene=doc)
        self.ok("play", enabled=True)
        state = self.ok("click", id="button")
        self.assertEqual(1, self.obj(state)["x"])
        self.assertEqual([.42, .72, 1, 1], self.obj(state)["color"])
        self.assertIn("broken", state["logs"][-1])
        self.assertNotIn("not committed", state["logs"])

    def test_infinite_script_is_stopped_and_vm_remains_usable(self):
        doc = fixture()
        doc["nodes"][2]["code"] = "while true do end"
        self.ok("load", scene=doc)
        self.ok("play", enabled=True)
        state = self.ok("click", id="button")
        self.assertIn("budget exceeded", state["logs"][-1])
        self.assertEqual(1, self.obj(self.ok("edit", id="orb", patch=dict(x=1)))["x"])
        self.ok("tick", dt=.1)

    def test_memory_budget_failure_rolls_back_and_recovers(self):
        doc = fixture()
        doc["nodes"][2]["code"] = 'self:set("x", 8); local s="x"; for i=1,30 do s=s..s end'
        self.ok("load", scene=doc)
        self.ok("play", enabled=True)
        state = self.ok("click", id="button")
        self.assertEqual(1, self.obj(state)["x"])
        self.assertIn("memory", state["logs"][-1])
        self.assertLessEqual(LIB.ws_memory(self.vm.handle), 64 * 1024 * 1024)
        self.ok("play", enabled=False)

    def test_sandbox_has_no_process_or_debug_api(self):
        doc = fixture()
        doc["nodes"][2]["code"] = 'assert(os==nil and io==nil and debug==nil and load==nil and require==nil); scene:get("button"):set("label", "worked")'
        self.ok("load", scene=doc)
        self.ok("play", enabled=True)
        state = self.ok("click", id="button")
        self.assertEqual("worked", self.obj(state, "button")["label"])

    def test_invalid_documents_and_cycle_leave_scene_unchanged(self):
        before = self.ok("export")["scene"]
        invalid = [dict(fixture(), version=2), dict(fixture(), objects=[])]
        cycle = fixture()
        cycle["links"].append(dict(**{"from": "script", "to": "color"}))
        invalid.append(cycle)
        for doc in invalid:
            self.assertFalse(self.vm.call("load", scene=doc)["ok"])
            self.assertEqual(before, self.ok("export")["scene"])

    def test_syntax_error_keeps_previous_script(self):
        doc = fixture()
        node = dict(doc["nodes"][2], code="if then")
        self.assertFalse(self.vm.call("node", node=node)["ok"])
        self.assertEqual(doc["nodes"][2]["code"], self.ok("export")["scene"]["nodes"][2]["code"])

    def test_delete_cleans_references_and_undo_restores_them(self):
        state = self.ok("delete", id="orb")
        self.assertEqual(["button"], [o["id"] for o in state["scene"]["objects"]])
        self.assertEqual(["click"], [n["id"] for n in state["scene"]["nodes"]])
        self.assertEqual([], state["scene"]["links"])
        self.assertEqual(3, len(self.ok("undo")["scene"]["nodes"]))

    def test_authored_round_trip_preserves_scripts_and_dirty_state(self):
        self.assertFalse(self.ok("snapshot")["dirty"])
        state = self.ok("edit", id="orb", patch=dict(label="雪 ⚙"))
        self.assertTrue(state["dirty"])
        doc = self.ok("export")["scene"]
        self.ok("saved", revision=state["revision"] - 1)
        self.assertTrue(self.ok("snapshot")["dirty"])
        self.ok("saved", revision=state["revision"])
        self.assertFalse(self.ok("snapshot")["dirty"])
        self.ok("load", scene=json.loads(json.dumps(doc)))
        self.assertEqual(doc, self.ok("export")["scene"])

    def test_tick_start_and_toggle(self):
        doc = fixture()
        doc["nodes"] = [dict(id="start", type="event", object="orb", event="start"),
                        dict(id="hide", type="toggle", object="orb"),
                        dict(id="tick", type="event", object="orb", event="tick"),
                        dict(id="move", type="script", object="orb", code='self:set("x", self:get("x") + dt)')]
        doc["links"] = [dict(**{"from": "start", "to": "hide"}), dict(**{"from": "tick", "to": "move"})]
        self.ok("load", scene=doc)
        state = self.ok("play", enabled=True)
        self.assertFalse(self.obj(state)["visible"])
        self.assertAlmostEqual(1.1, self.obj(self.ok("tick", dt=2))["x"])

    def test_each_vm_is_independent(self):
        other = Runtime()
        self.addCleanup(other.close)
        self.assertEqual([], other.call("snapshot")["objects"])
        self.ok("edit", id="orb", patch=dict(x=98))
        self.assertEqual([], other.call("snapshot")["objects"])

    def test_graph_layout_drag_does_not_restart_live_simulation(self):
        self.ok("play", enabled=True)
        self.ok("tick", dt=.1)
        node = copy.deepcopy(self.ok("snapshot")["scene"]["nodes"][2])
        node.update(x=500, y=40, duration=1)  # managed DTO supplies default fields
        state = self.ok("node", node=node, group="graph-drag")
        self.assertAlmostEqual(.1, state["time"])

    def test_box_geometry_rotates_about_object_center(self):
        x, y, z = c.c_float(), c.c_float(), c.c_float()
        LIB.ws_box_vertex(7, 1, 2, 3, 2, 4, 6, math.pi / 2, c.byref(x), c.byref(y), c.byref(z))
        self.assertAlmostEqual(-2, x.value, places=5)
        self.assertAlmostEqual(4, y.value, places=5)
        self.assertAlmostEqual(4, z.value, places=5)


if __name__ == "__main__":
    unittest.main(verbosity=2)
