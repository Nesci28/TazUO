#pragma once
#include <SDL3/SDL.h>

/* Local adapter for FNA3D's remaining SDL2 UIKit framebuffer query. Only this
 * private build target includes the header; no SDL2 ABI is linked into iOS. */
enum { SDL_SYSWM_UIKIT = 1 };
typedef struct SDL_SysWMinfo
{
    int version;
    int subsystem;
    struct
    {
        struct { unsigned int framebuffer, colorbuffer; } uikit;
    } info;
} SDL_SysWMinfo;

#define SDL_VERSION(version_ptr) (*(version_ptr) = SDL_GetVersion())

static inline bool SDL_GetWindowWMInfo(SDL_Window *window, SDL_SysWMinfo *info)
{
    SDL_PropertiesID props = SDL_GetWindowProperties(window);
    if (!SDL_GetPointerProperty(props, SDL_PROP_WINDOW_UIKIT_WINDOW_POINTER, NULL))
    {
        info->subsystem = 0;
        return false;
    }
    info->subsystem = SDL_SYSWM_UIKIT;
    info->info.uikit.framebuffer = (unsigned int)SDL_GetNumberProperty(
        props, SDL_PROP_WINDOW_UIKIT_OPENGL_FRAMEBUFFER_NUMBER, 0);
    info->info.uikit.colorbuffer = (unsigned int)SDL_GetNumberProperty(
        props, SDL_PROP_WINDOW_UIKIT_OPENGL_RENDERBUFFER_NUMBER, 0);
    return true;
}
