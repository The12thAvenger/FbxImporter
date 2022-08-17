#include "pch.h"
#include "Util.h"

void Util::FbxVectorToArray(const FbxVector4& sourceVector, array<float>^ targetArray)
{
	for (int i = 0; i < targetArray->Length; i++)
	{
		targetArray[i] = sourceVector.mData[i];
	}
}

