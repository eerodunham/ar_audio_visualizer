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

template <typename T>
struct NotNullSpan {
	NotNullSpan() = delete;

	NotNullSpan(gsl::not_null<T*> ptr, const size_t size) : span(ptr.get(), size) {}

	gsl::not_null<T*> Data() const noexcept { return span.data(); }

	size_t Size() const noexcept { return span.size(); }

	size_t SizeBytes() const noexcept { return span.size_bytes(); }

	const std::span<T>& Inner() const noexcept { return span; }

private:
	std::span<T> span;
};

struct FFTProperties {
	FFTProperties() = delete;

	static std::optional<FFTProperties> Create(const NotNullSpan<const float>& signal, const NotNullSpan<float>& out, const NotNullSpan<const float>& window, const std::string_view& fileName) {
		if (signal.Size() == 0 || (signal.Size() != out.Size()) || window.Size() == 0) [[unlikely]] {
			return std::nullopt;
		}
		if (!std::filesystem::exists(fileName)) {
			DispatchFFT(signal.Data(), out.Data(), signal.Size(), window.Size(), 0, FFTW_MEASURE);
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

		return FFTProperties(signal.Data(), out.Data(), window.Data(), window.Size(), signal.Size());
	}

	void CalculateDFT(const size_t intervalOffset) {
		DispatchFFT(signalPtr, outPtr, length, interval, intervalOffset, FFTW_WISDOM_ONLY);
	}

	size_t Size() const noexcept {
		return length;
	}

private:
	gsl::not_null<const float*> signalPtr;
	gsl::not_null<const float*> windowPtr;
	gsl::not_null<float*> outPtr;
	size_t interval;
	size_t length;

	FFTProperties(gsl::not_null<const float*> signalPtr, gsl::not_null<float*> outPtr, gsl::not_null<const float*> windowPtr, const size_t interval, const size_t length) :
		signalPtr(signalPtr), outPtr(outPtr), windowPtr(windowPtr), interval(interval), length(length) {}

	static void DispatchFFT(gsl::not_null<const float*> signalPtr, gsl::not_null<float*> outPtr, const size_t length, const size_t interval, const size_t intervalOffset, const int32_t flags) {
		auto fftwInCast  = const_cast<float*>(signalPtr.get());

		fftwf_plan plan  = fftwf_plan_r2r_1d(interval, fftwInCast, outPtr.get(), FFTW_REDFT11, flags);
		fftwf_execute(plan);
	}
};

LibFunc bool CreateFFTProps(const float* signalPtr, float* outPtr, const float* windowPtr, const size_t length, const size_t interval, const char* fileName, FFTProperties& props) {
	NotNullSpan<const float> signal(signalPtr, length);
	NotNullSpan<float>		 out(outPtr,	   length);
	NotNullSpan<const float> window(windowPtr, interval);

	auto result = FFTProperties::Create(signal, out, window, std::string_view(fileName));
	if (result.has_value()) {
		props = result.value();
		return true;
	}
	return false;
}

LibFunc void ComputeFFT(FFTProperties& properties, const size_t intervalOffset) {
	properties.CalculateDFT(intervalOffset);
}