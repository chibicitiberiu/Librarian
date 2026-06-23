# CMakeList.txt : CMake project for meta-cli, include source and define
# project specific logic here.
#
cmake_minimum_required (VERSION 3.16)

# Enable Hot Reload for MSVC compilers if supported.
if (POLICY CMP0141)
  cmake_policy(SET CMP0141 NEW)
  set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
endif()

project ("meta-cli")

include_directories("import/argparse/include"
					"import/json/single_include")

# Add source to this project's executable.
add_executable (meta-cli "main.cpp" "main.h")

if (CMAKE_VERSION VERSION_GREATER 3.12)
  set_property(TARGET meta-cli PROPERTY CXX_STANDARD 20)
endif()

# TODO: Add tests and install targets if needed.

# Locate the FFmpeg libraries. On Linux/macOS use pkg-config, which also
# provides the correct include directories (e.g. Fedora installs the headers
# under /usr/include/ffmpeg). Fall back to find_library on platforms without
# pkg-config (e.g. Windows/MSVC with vcpkg).
find_package(PkgConfig QUIET)
if (PkgConfig_FOUND)
  pkg_check_modules(FFMPEG REQUIRED IMPORTED_TARGET libavformat libavcodec libavutil)
  target_link_libraries(meta-cli PRIVATE PkgConfig::FFMPEG)
else()
  find_library(LIBAVFORMAT NAMES avformat libavformat REQUIRED)
  find_library(LIBAVUTIL NAMES avutil libavutil REQUIRED)
  find_library(LIBAVCODEC NAMES avcodec libavcodec REQUIRED)
  target_link_libraries(meta-cli PRIVATE ${LIBAVFORMAT} ${LIBAVUTIL} ${LIBAVCODEC})
endif()