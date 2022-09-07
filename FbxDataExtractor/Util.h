#pragma once
#include <fbxsdk.h>
#include <vector>

namespace FbxDataExtractor
{
	template<int ArraySize, typename T>
	array<float>^ FbxVectorToArray(const T& sourceVector)
	{
		array<float>^ targetArray = gcnew array<float>(ArraySize);
		for (int i = 0; i < ArraySize; i++)
		{
			const int arrSize = *(&sourceVector.mData + 1) - sourceVector.mData;
			if (i >= arrSize)
			{
				targetArray[i] = 0.0f;
			}
			targetArray[i] = sourceVector.mData[i];
		}

		return targetArray;
	}

	template<typename T>
	bool IsAllEqual(std::vector<T> vector)
	{
		return std::equal(vector.begin() + 1, vector.end(), vector.begin());
	}

	template<typename T>
	int GetVertexIndex(const FbxLayerElementTemplate<T>* element, const int* vertexIndices, const int index)
	{
		switch (element->GetMappingMode())
		{
		case FbxLayerElement::eByControlPoint:
			return index;
		case FbxLayerElement::eByPolygonVertex:
			return vertexIndices[index];
		default: throw gcnew IO::InvalidDataException("Unsupported mapping mode.");
		}
	}

	template<typename T>
	std::vector<std::vector<T>> GetElementListByControlPoint(const FbxLayerElementTemplate<T>* element, const int* vertexIndices, const int numVertices)
	{
		std::vector<std::vector<T>> elementListPerControlPoint(numVertices);

		const FbxLayerElementArrayTemplate<T>& directArray = element->GetDirectArray();
		const FbxLayerElementArrayTemplate<int>& indexArray = element->GetIndexArray();

		switch (element->GetReferenceMode())
		{
		case FbxLayerElement::eDirect:
			for (int i = 0; i < directArray.GetCount(); i++)
			{
				const int vertexIndex = GetVertexIndex(element, vertexIndices, i);
				elementListPerControlPoint.at(vertexIndex).push_back(directArray.GetAt(i));
			}
			break;
		case FbxLayerElement::eIndexToDirect:
			for (int i = 0; i < indexArray.GetCount(); i++)
			{
				const int directIndex = indexArray.GetAt(i);
				const int vertexIndex = GetVertexIndex(element, vertexIndices, i);
				elementListPerControlPoint.at(vertexIndex).push_back(directArray.GetAt(directIndex));
			}
			break;
		default: throw gcnew IO::InvalidDataException("Unsupported reference mode.");
		}

		return elementListPerControlPoint;
	}

	inline bool IsIdentical(FbxVertexData^ v1, FbxVertexData^ v2)
	{
		if (!Linq::Enumerable::SequenceEqual(gcnew  List<float>(v1->Position), gcnew  List<float>(v2->Position))
			|| Linq::Enumerable::SequenceEqual(gcnew  List<float>(v1->Normal), gcnew  List<float>(v2->Normal))
			|| v1->Tangents->Count != v2->Tangents->Count
			|| v1->UVs->Count != v2->UVs->Count)
		{
			return false;
		}

		for (int i = 0; i < v1->Tangents->Count; ++i)
		{
			if (!Linq::Enumerable::SequenceEqual(gcnew  List<float>(v1->Tangents[i]), gcnew  List<float>(v2->Tangents[i])))
			{
				return false;
			}
		}

		for (int i = 0; i < v1->UVs->Count; ++i)
		{
			if (!Linq::Enumerable::SequenceEqual(gcnew  List<float>(v1->UVs[i]), gcnew  List<float>(v2->UVs[i])))
			{
				return false;
			}
		}

		return true;
	}

	inline void DecrementVertexIndicesAbove(const int limit, List<int>^ vertexIndices)
	{
		for (int i = 0; i < vertexIndices->Count; ++i)
		{
			if (vertexIndices[i] > limit)
			{
				vertexIndices[i] -= 1;
			}
		}
	}
}

