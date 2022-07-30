#include "pch.h"
#include "FbxVertexData.h"

FbxDataExtractor::FbxVertexData::FbxVertexData()
{
	Position = gcnew array<float>(3);
	Normal = gcnew array<float>(3);
	Tangents = gcnew List<array<float>^>();
	UVs = gcnew List<array<float>^>();
	BoneNames = gcnew List<String^>();
	BoneWeights = gcnew List<float>();
}
