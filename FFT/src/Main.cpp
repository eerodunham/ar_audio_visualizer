// FFTW Includes
#include <fftw/fftw3.h>

// TL Includes
#include <tl/expected.hpp>

// GSL Includes
#include <gsl/pointers>

// STL Includes
#include <array>
#include <complex>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <optional>
#include <span>

using std::cout, std::endl;

#ifdef _WIN32
#define LibFunc extern "C" __declspec(dllexport)
#elifdef linux
#define LibFunc extern "C" __attribute__((visibility("default")))
#endif

struct FFTProperties {
	FFTProperties() = delete;

	static std::optional<FFTProperties> Create(gsl::not_null<const float*> signalPtr, gsl::not_null<float*> complexPtr, const size_t length, const std::string_view& fileName) {
		if (length == 0) [[unlikely]] {
			return std::nullopt;
		}
		if (!std::filesystem::exists(fileName)) {
			DispatchFFT(signalPtr, complexPtr, length, FFTW_MEASURE);
			char* wisdomData = fftwf_export_wisdom_to_string();
			if (wisdomData == nullptr) [[unlikely]] {
				return std::nullopt;
			}
			std::ofstream fileStream(fileName.data());
			fileStream << wisdomData;
			fileStream.close();
		}
		else {
			std::ifstream fileStream(fileName.data());
			std::string fileContents = std::string(std::istreambuf_iterator<char>(fileStream), std::istreambuf_iterator<char>());
			fileStream.close();
			fftwf_import_wisdom_from_string(fileContents.data());
		}

		return FFTProperties(signalPtr, complexPtr, length);
	}

	void CalculateDFT() {
		DispatchFFT(signalPtr, outPtr, length, FFTW_WISDOM_ONLY);
	}

	size_t Size() const noexcept {
		return length;
	}

private:
	gsl::not_null<const float*> signalPtr;
	gsl::not_null<float*> outPtr;
	size_t length;

	FFTProperties(gsl::not_null<const float*> signalPtr, gsl::not_null<float*> outPtr, const size_t length) : signalPtr(signalPtr), outPtr(outPtr), length(length) {}

	static void DispatchFFT(gsl::not_null<const float*> signalPtr, gsl::not_null<float*> outPtr, const size_t length, const int32_t flags) {
		auto fftwInCast  = const_cast<float*>(signalPtr.get());
		fftwf_plan plan  = fftwf_plan_r2r_1d(length, fftwInCast, outPtr, FFTW_REDFT11, flags);
		fftwf_execute(plan);
	}
};

LibFunc bool CreateFFTProps(const float* signalPtr, float* outPtr, const size_t length, const char* fileName, FFTProperties& props) {
	auto result = FFTProperties::Create(signalPtr, outPtr, length, std::string_view(fileName));
	if (result.has_value()) {
		props = result.value();
		return true;
	}
	return false;
}

LibFunc void ComputeFFT(FFTProperties& properties) {
	properties.CalculateDFT();
}