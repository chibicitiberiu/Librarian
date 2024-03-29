﻿# CMakeList.txt : CMake project for meta-cli, include source and define
# project specific logic here.
#
cmake_minimum_required (VERSION 3.8)

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
find_library(LIBAVFORMAT NAMES avformat libavformat)
find_library(LIBAVUTIL NAMES avutil libavutil)
find_library(LIBAVCODEC NAMES avcodec libavcodec)
target_link_libraries(meta-cli PRIVATE avformat avutil avcodec)