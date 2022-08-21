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
	std::vector<T> GetElementByVertex(const FbxLayerElementTemplate<T>* element, const int* vertexIndices, const int numVertices)
	{
		std::vector<T> elementsPerVertex(numVertices);

		const FbxLayerElementArrayTemplate<T>& directArray = element->GetDirectArray();
		const FbxLayerElementArrayTemplate<int>& indexArray = element->GetIndexArray();

		switch (element->GetReferenceMode())
		{
		case FbxLayerElement::eDirect:
			for (int i = 0; i < directArray.GetCount(); i++)
			{
				const int vertexIndex = GetVertexIndex(element, vertexIndices, i);
				elementsPerVertex.at(vertexIndex) = directArray.GetAt(i);
			}
			break;
		case FbxLayerElement::eIndexToDirect:
			for (int i = 0; i < indexArray.GetCount(); i++)
			{
				const int directIndex = indexArray.GetAt(i);
				const int vertexIndex = GetVertexIndex(element, vertexIndices, i);
				elementsPerVertex.at(vertexIndex) = directArray.GetAt(directIndex);
			}
			break;
		default: throw gcnew IO::InvalidDataException("Unsupported reference mode.");
		}

		return elementsPerVertex;
	}
}

