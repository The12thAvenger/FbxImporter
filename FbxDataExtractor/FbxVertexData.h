#pragma once

using namespace System;
using namespace System::Collections::Generic;

namespace FbxDataExtractor {
	public ref class FbxVertexData
	{
	public:
		FbxVertexData();

		array<float>^ Position;

		array<float>^ Normal;

		List<array<float>^>^ Tangents;

		List<array<float>^>^ UVs;

		List<String^>^ BoneNames;

		List<float>^ BoneWeights;
	};
}


