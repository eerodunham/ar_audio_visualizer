include(CMakePrintHelpers)
include(FetchContent)

cmake_minimum_required(VERSION 3.25.2)
project("AudioVisFFT" LANGUAGES CXX)

# Enable Hot Reload for MSVC compilers if supported.
if (POLICY CMP0141)
	cmake_policy(SET CMP0141 NEW)
	set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")

endif()

# Language Standards
if (CMAKE_CXX_COMPILER_VERSION VERSION_GREATER_EQUAL 19.12.25835)
	set(CMAKE_CXX23_STANDARD_COMPILE_OPTION  "/std:c++latest")
	set(CMAKE_CXX23_EXTENSION_COMPILE_OPTION "/std:c++latest")
endif()

file(GLOB AllSources
    "${PROJECT_SOURCE_DIR}/include/*.hpp"
	"${PROJECT_SOURCE_DIR}/src/*.cpp"
)

source_group(TREE ${CMAKE_CURRENT_SOURCE_DIR} FILES ${AllSources})

include_directories(
    ${PROJECT_SOURCE_DIR}/include
	${PROJECT_SOURCE_DIR}/include/shared
)

# Find Packagesd
find_package(fmt CONFIG REQUIRED)
find_package(FFTW3 REQUIRED)
find_package(Microsoft.GSL CONFIG REQUIRED)

add_library(${PROJECT_NAME} SHARED ${AllSources})

set_property(TARGET ${PROJECT_NAME} PROPERTY CXX_STANDARD 23)

target_link_libraries(${PROJECT_NAME} INTERFACE
	fmt::fmt-header-only
    Microsoft.GSL::GSL
	FFTW3::fftw3
)