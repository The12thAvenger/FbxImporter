#pragma once
#include <fbxsdk.h>

ref class Util
{
public:
	static void FbxVectorToArray(const FbxVector4& sourceVector, array<float>^ targetArray);
};

