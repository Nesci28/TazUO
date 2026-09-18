cmake_minimum_required(VERSION 3.20)
project(TazUOiOSLibraries C)

if(NOT CMAKE_SYSTEM_NAME STREQUAL "iOS")
  message(FATAL_ERROR "This wrapper builds iOS libraries only")
endif()
if(NOT DEFINED TAZUO_ROOT OR NOT DEFINED SDL3_SOURCE OR NOT DEFINED SDL3_ARCHIVE)
  message(FATAL_ERROR "Provide TAZUO_ROOT, SDL3_SOURCE and SDL3_ARCHIVE")
endif()

set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(BUILD_SDL3 ON CACHE BOOL "" FORCE)
set(FAUDIO_INSTALL OFF CACHE BOOL "" FORCE)
set(SDL3_INCLUDE_DIRS "${SDL3_SOURCE}/include")
set(SDL3_LIBRARIES "${SDL3_ARCHIVE}")
add_subdirectory("${TAZUO_ROOT}/external/FNA/lib/FNA3D" FNA3D)
add_subdirectory("${TAZUO_ROOT}/external/FNA/lib/FAudio" FAudio)

# The upstream CMake projects set a macOS deployment version on all Apple
# platforms. Override the Xcode target settings here, without patching them.
foreach(target FNA3D mojoshader FAudio-static)
  set_target_properties(${target} PROPERTIES
    XCODE_ATTRIBUTE_IPHONEOS_DEPLOYMENT_TARGET "15.0"
    XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED "NO"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib")
endforeach()
target_compile_definitions(FNA3D PRIVATE SDL_VIDEO_DRIVER_UIKIT=1)
target_include_directories(FNA3D PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/uikit-sdl3")
