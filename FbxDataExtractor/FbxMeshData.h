#pragma once
#include <fbxsdk.h>
#include "FbxVertexData.h"

using namespace System;
using namespace System::Collections::Generic;

namespace FbxDataExtractor {
	public ref class FbxMeshData
	{
	public:
		FbxMeshData(const char* name);

		String^ Name;

		List<int>^ VertexIndices;

		List<FbxVertexData^>^ VertexData;

		static List<FbxMeshData^>^ Import(String^ path);

	private:
		static FbxMeshData^ Import(FbxMesh* fbxMesh);

		static bool VertexDataIsNull(FbxVertexData^ vertexData);
	};
}


