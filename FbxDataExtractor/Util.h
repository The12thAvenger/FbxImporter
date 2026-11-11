#pragma once
#include <fbxsdk.h>
#include <vector>
#include <string>
#include <cstring>
#include <fbxsdk/scene/geometry/fbxlayer.h>

namespace FbxDataExtractor
{
	template <class T>
	bool SequenceEqual(const std::vector<T>& vector1, const std::vector<T>& vector2)
	{
		if (vector1.size() != vector2.size()) return false;
		for (int i = 0; i < vector1.size(); ++i)
		{
			if (vector1[i] != vector2[i]) return false;
		}

		return true;
	}

	// The FBX SDK takes and returns text as UTF-8, whatever the system code page is.
	inline std::string ToUtf8(System::String^ text)
	{
		array<System::Byte>^ bytes = System::Text::Encoding::UTF8->GetBytes(text);
		if (bytes->Length == 0) return std::string();
		pin_ptr<System::Byte> pinned = &bytes[0];
		return std::string(reinterpret_cast<const char*>(pinned), bytes->Length);
	}

	inline System::String^ FromUtf8(const char* text)
	{
		if (!text) return System::String::Empty;
		return gcnew System::String(text, 0, static_cast<int>(strlen(text)), System::Text::Encoding::UTF8);
	}

	inline Vector4 FbxColorToVector4(const FbxColor& sourceVector)
	{
		return Vector4(static_cast<float>(sourceVector[0]), static_cast<float>(sourceVector[1]), static_cast<float>(sourceVector[2]), static_cast<float>(sourceVector[3]));
	}

	inline Vector4 FbxVector4ToVector4(const FbxVector4& sourceVector)
	{
		return Vector4(static_cast<float>(sourceVector[0]), static_cast<float>(sourceVector[1]), static_cast<float>(sourceVector[2]), static_cast<float>(sourceVector[3]));
	}

	inline Vector3 FbxVector4ToVector3(const FbxVector4& sourceVector)
	{
		return Vector3(static_cast<float>(sourceVector[0]), static_cast<float>(sourceVector[1]), static_cast<float>(sourceVector[2]));
	}

	inline Vector2 FbxVector2ToVector2(const FbxVector2& sourceVector)
	{
		return Vector2(static_cast<float>(sourceVector[0]), static_cast<float>(sourceVector[1]));
	}

	inline int GetMappedIndex(const FbxLayerElement::EMappingMode mappingMode, const int controlPointIndex, const int polygonVertexIndex)
	{
		switch (mappingMode)
		{
		case FbxLayerElement::eByControlPoint:
			return controlPointIndex;
		case FbxLayerElement::eByPolygonVertex:
			return polygonVertexIndex;
		default: throw gcnew IO::InvalidDataException("Unsupported mapping mode.");
		}
	}

	template <typename T>
	T GetLayerElementValue(const FbxLayerElementTemplate<T>& element, const int controlPointIndex, const int polygonVertexIndex)
	{
		int mappedIndex = GetMappedIndex(element.GetMappingMode(), controlPointIndex, polygonVertexIndex);
		switch (element.GetReferenceMode())
		{
		case FbxLayerElement::eDirect:
			return element.GetDirectArray().GetAt(mappedIndex);
		case FbxLayerElement::eIndexToDirect:
		case FbxLayerElement::eIndex:
			return element.GetDirectArray().GetAt(element.GetIndexArray().GetAt(mappedIndex));
		default: throw gcnew IO::InvalidDataException("Unsupported reference mode.");
		}
	}
}
