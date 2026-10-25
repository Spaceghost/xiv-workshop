// Headless test hooks and a software capture of actual ImGui triangles.
// Compiled only for tests, never shipped with the plugin.
#include "imgui.h"
#include "imgui_internal.h"
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <string>
#include <unordered_map>
#include <vector>

struct Item { ImGuiID id; ImRect rect; std::string label, window; };
static std::vector<Item> items;
static std::unordered_map<ImGuiID,std::string> labels;
void ImGuiTestEngineHook_ItemAdd(ImGuiContext* ctx, const ImRect& rect, ImGuiID id) {
    items.push_back({id, rect, labels[id], ctx->CurrentWindow->Name});
}
void ImGuiTestEngineHook_ItemInfo(ImGuiContext* ctx, ImGuiID id, const char* label, ImGuiItemStatusFlags) {
    labels[id]=label;
    for (auto it = items.rbegin(); it != items.rend(); ++it)
        if (it->id == id) { it->label = label; return; }
}
void ImGuiTestEngineHook_Log(ImGuiContext*, const char*, ...) {}
const char* ImGuiTestEngine_FindItemDebugLabel(ImGuiContext*, ImGuiID) { return ""; }
extern "C" void ws_ui_begin() { items.clear(); labels.clear(); GImGui->TestEngineHookItems = true; }
extern "C" bool ws_ui_item(const char* label, const char* window, int occurrence, float* x, float* y) {
    for (const auto& item : items)
        if (item.label == label && item.window.find(window) != std::string::npos && occurrence-- == 0) {
            *x = (item.rect.Min.x + item.rect.Max.x) / 2;
            *y = (item.rect.Min.y + item.rect.Max.y) / 2;
            return true;
        }
    return false;
}
extern "C" void ws_ui_items() {
    for (const auto& item : items) if (!item.label.empty())
        std::printf("%s | %s | %.0f,%.0f\n", item.window.c_str(), item.label.c_str(), item.rect.Min.x, item.rect.Min.y);
    std::fflush(stdout);
}

static float cross(ImVec2 a, ImVec2 b, ImVec2 p) {
    return (p.x-a.x)*(b.y-a.y) - (p.y-a.y)*(b.x-a.x);
}
extern "C" void ws_ui_capture(const char* path) {
    ImDrawData* data = ImGui::GetDrawData();
    const int w = (int)data->DisplaySize.x, h = (int)data->DisplaySize.y;
    std::vector<unsigned char> image(w*h*3);
    for (int i=0; i<w*h; ++i) { image[i*3]=20; image[i*3+1]=24; image[i*3+2]=33; }
    unsigned char* atlas; int aw,ah;
    ImGui::GetIO().Fonts->GetTexDataAsRGBA32(0,&atlas,&aw,&ah);
    for (int li=0; li<data->CmdListsCount; ++li) {
        auto list=data->CmdLists[li];
        for (const auto& command : list->CmdBuffer) {
            if (command.UserCallback) continue;
            for (unsigned int i=0; i<command.ElemCount; i+=3) {
                const auto& a=list->VtxBuffer[list->IdxBuffer[command.IdxOffset+i]+command.VtxOffset];
                const auto& b=list->VtxBuffer[list->IdxBuffer[command.IdxOffset+i+1]+command.VtxOffset];
                const auto& c=list->VtxBuffer[list->IdxBuffer[command.IdxOffset+i+2]+command.VtxOffset];
                float area=cross(a.pos,b.pos,c.pos); if (std::abs(area)<1e-5f) continue;
                int x0=std::max(0,(int)std::floor(std::max(command.ClipRect.x,std::min({a.pos.x,b.pos.x,c.pos.x}))));
                int y0=std::max(0,(int)std::floor(std::max(command.ClipRect.y,std::min({a.pos.y,b.pos.y,c.pos.y}))));
                int x1=std::min(w,(int)std::ceil(std::min(command.ClipRect.z,std::max({a.pos.x,b.pos.x,c.pos.x}))));
                int y1=std::min(h,(int)std::ceil(std::min(command.ClipRect.w,std::max({a.pos.y,b.pos.y,c.pos.y}))));
                for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++) {
                    ImVec2 p(x+.5f,y+.5f);
                    float ca=cross(b.pos,c.pos,p)/area, cb=cross(c.pos,a.pos,p)/area, cc=1-ca-cb;
                    if(ca<0 || cb<0 || cc<0) continue;
                    int u=std::max(0,std::min(aw-1,(int)((a.uv.x*ca+b.uv.x*cb+c.uv.x*cc)*aw)));
                    int v=std::max(0,std::min(ah-1,(int)((a.uv.y*ca+b.uv.y*cb+c.uv.y*cc)*ah)));
                    auto tex=atlas+(v*aw+u)*4;
                    float alpha=tex[3]/255.f*((a.col>>24)*ca+(b.col>>24)*cb+(c.col>>24)*cc)/255.f;
                    for(int ch=0;ch<3;ch++) {
                        float color=((a.col>>(ch*8))&255)*ca+((b.col>>(ch*8))&255)*cb+((c.col>>(ch*8))&255)*cc;
                        auto& dst=image[(y*w+x)*3+ch];
                        dst=(unsigned char)(color*tex[ch]/255.f*alpha+dst*(1-alpha));
                    }
                }
            }
        }
    }
    auto file=std::fopen(path,"wb");
    if(file) { std::fprintf(file,"P6\n%d %d\n255\n",w,h); std::fwrite(image.data(),1,image.size(),file); std::fclose(file); }
}
