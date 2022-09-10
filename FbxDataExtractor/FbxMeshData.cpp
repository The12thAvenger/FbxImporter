#include "pch.h"
#include <msclr/marshal.h>
#include <vector>

#include "FbxMeshData.h"
#include "Util.h"

namespace FbxDataExtractor {
    FbxMeshData::FbxMeshData(const char* name)
    {
        Name = gcnew String(name);
        VertexIndices = gcnew List<int>();
        VertexData = gcnew List<FbxVertexData^>();
    }

    List<FbxMeshData^>^ FbxMeshData::Import(String^ path)
    {
        FbxManager* fbxManager = FbxManager::Create();

        FbxIOSettings* ios = FbxIOSettings::Create(fbxManager, IOSROOT);
        fbxManager->SetIOSettings(ios);
        FbxImporter* importer = FbxImporter::Create(fbxManager, "");

        msclr::interop::marshal_context^ context = gcnew msclr::interop::marshal_context();
        const char* pathPtr = context->marshal_as<const char*>(path);

        importer->Initialize(pathPtr, -1, ios);

        delete context;

        FbxScene* scene = FbxScene::Create(fbxManager, "");
        importer->Import(scene);
        importer->Destroy();
        ios->Destroy();

        List<FbxMeshData^>^ meshList = gcnew List<FbxMeshData^>();
        for (int i = 0; i < scene->GetNodeCount(); i++)
        {
            FbxMesh* mesh = scene->GetNode(i)->GetMesh();
            if (mesh == nullptr) {
                continue;
            }
            meshList->Add(Import(mesh));
        }

        fbxManager->Destroy();

        return meshList;
    }
    
    FbxMeshData^ FbxMeshData::Import(FbxMesh* fbxMesh)
    {
        const char* meshName = fbxMesh->GetNode()->GetName();
        if (!fbxMesh->IsTriangleMesh()) {
            throw gcnew IO::InvalidDataException(gcnew String("Fbx mesh \"" + gcnew String(meshName) + "\" is not triangulated. Please triangulate all meshes."));
        }

        fbxMesh->GenerateNormals();
        fbxMesh->GenerateTangentsDataForAllUVSets();

        const int* vertexIndices = fbxMesh->GetPolygonVertices();

        // get all vertex data per control point, must account for multiple unique normals/tangents/uvs
        const int numControlPoints = fbxMesh->GetControlPointsCount();
        const std::vector<std::vector<FbxVector4>> normalListPerControlPoint = GetElementListByControlPoint(fbxMesh->GetElementNormal(), vertexIndices, numControlPoints);

        std::vector<std::vector<std::vector<FbxVector4>>> tangentListPerControlPointLayers{ };
        const int numTangents = fbxMesh->GetElementTangentCount();
        for (int i = 0; i < numTangents; i++)
        {
            tangentListPerControlPointLayers.push_back(GetElementListByControlPoint(fbxMesh->GetElementTangent(i), vertexIndices, numControlPoints));
        }

        std::vector<std::vector<std::vector<FbxVector2>>> uvListPerControlPointLayers{ };
        const int numUvs = fbxMesh->GetElementUVCount();
        for (int i = 0; i < numUvs; i++)
        {
            uvListPerControlPointLayers.push_back(GetElementListByControlPoint(fbxMesh->GetElementUV(i), vertexIndices, numControlPoints));
        }

        array<int>^ numVertexDataPerControlPoint = gcnew array<int>(numControlPoints);
        List<int>^ vertexIndicesList = gcnew List<int>();
        for (int i = 0; i < fbxMesh->GetPolygonVertexCount(); i++)
        {
            numVertexDataPerControlPoint[vertexIndices[i]] += 1;
            vertexIndicesList->Add(vertexIndices[i]);
        }

        // loop over control points and create all vertex data
        array<array<FbxVertexData^>^>^ vertexDataPerControlPoint = gcnew array<array<FbxVertexData^>^>(numControlPoints);
        for (int i = 0; i < numControlPoints; i++)
        {
            if (numVertexDataPerControlPoint[i] == 0) {
                vertexDataPerControlPoint[i] = nullptr;
                continue;
            }
            vertexDataPerControlPoint[i] = gcnew array<FbxVertexData^>(numVertexDataPerControlPoint[i]);

            for (int j = 0; j < vertexDataPerControlPoint[i]->Length; ++j)
            {
                FbxVertexData^ vertexData = gcnew FbxVertexData();
                vertexDataPerControlPoint[i][j] = vertexData;

                FbxVector4 position = fbxMesh->GetControlPointAt(i);
                vertexData->Position = FbxVectorToArray<3>(position);

                FbxVector4 normal = normalListPerControlPoint.at(i).at(j);
                vertexData->Normal = FbxVectorToArray<3>(normal);

                for (int k = 0; k < numTangents; k++)
                {
                    FbxVector4 tangent = tangentListPerControlPointLayers.at(k).at(i).at(j);
                    vertexData->Tangents->Add(FbxVectorToArray<4>(tangent));
                }

                for (int k = 0; k < numUvs; k++)
                {
                    FbxVector2 uv = uvListPerControlPointLayers.at(k).at(i).at(j);
                    vertexData->UVs->Add(FbxVectorToArray<3>(uv));
                }
            }
        }

        // get skin data for each control point, this is the same for all vertices belonging to a given control point
        FbxSkin* skin = nullptr;
        for (int i = 0; i < fbxMesh->GetDeformerCount(); i++)
        {
            if (fbxMesh->GetDeformer(i)->GetDeformerType() == FbxDeformer::eSkin) {
                skin = static_cast<FbxSkin*>(fbxMesh->GetDeformer(i));
                break;
            }
        }

        if (skin != nullptr) {
            for (int i = 0; i < skin->GetClusterCount(); i++)
            {
                FbxCluster* cluster = skin->GetCluster(i);
                const int* controlPointIndices = cluster->GetControlPointIndices();
                const double* controlPointWeights = cluster->GetControlPointWeights();
                for (int j = 0; j < cluster->GetControlPointIndicesCount(); j++)
                {
                    if (vertexDataPerControlPoint[controlPointIndices[j]] == nullptr) {
                        continue;
                    }
	                for (int k = 0; k < vertexDataPerControlPoint[controlPointIndices[j]]->Length; k++)
	                {
                        FbxVertexData^ vertexData = vertexDataPerControlPoint[controlPointIndices[j]][k];

                        String^ boneName = gcnew String(cluster->GetLink()->GetName());
                        vertexData->BoneNames->Add(boneName);

                        const float boneWeight = controlPointWeights[j];
                        vertexData->BoneWeights->Add(boneWeight);
	                }
                }
            }
        }

        // flatten vertex data, remove duplicates and adjust vertex indices

        array<Queue<FbxVertexData^>^>^ vertexDataQueuesPerControlPoint = gcnew array<Queue<FbxVertexData^>^>(vertexDataPerControlPoint->Length);
        for (int i = 0; i < vertexDataQueuesPerControlPoint->Length; ++i)
        {
            if (vertexDataPerControlPoint[i] == nullptr)
            {
                continue;
            }
            vertexDataQueuesPerControlPoint[i] = gcnew Queue<FbxVertexData^>(vertexDataPerControlPoint[i]);
        }
        
        List<FbxVertexData^>^ vertexDataList = gcnew List<FbxVertexData^>(gcnew array<FbxVertexData^>(numControlPoints));
        for (int i = 0; i < vertexIndicesList->Count; ++i)
        {
            int controlPointIndex = vertexIndicesList[i];

            FbxVertexData^ vertexData = vertexDataQueuesPerControlPoint[controlPointIndex]->Dequeue();
            FbxVertexData^ vertexDataAtControlPointIndex = vertexDataList[controlPointIndex];
            if(vertexDataAtControlPointIndex == nullptr)
            {
                vertexDataList[controlPointIndex] = vertexData;
                continue;
            }

            if (IsIdentical(vertexData, vertexDataAtControlPointIndex))
            {
                continue;
            }

            /*int vertexIndex = -1;
            for (int j = numControlPoints; j < vertexDataList->Count; ++j)
            {
                if (vertexDataList[j] == nullptr)
                {
                    continue;
                }

                if (IsIdentical(vertexData, vertexDataList[j]))
                {
                    vertexIndex = j;
                    break;
                }
            }

            if (vertexIndex != -1)
            {
                vertexIndicesList[i] = vertexIndex;
                continue;
            }*/

            vertexIndicesList[i] = vertexDataList->Count;
            vertexDataList->Add(vertexData);
        }

        for (int i = 0; i < vertexDataList->Count; ++i)
        {
	        while (i < vertexDataList->Count && vertexDataList[i] == nullptr)
	        {
                DecrementVertexIndicesAbove(i, vertexIndicesList);
                vertexDataList->RemoveAt(i);
	        }
        }

        FbxMeshData^ meshData = gcnew FbxMeshData(meshName);
        meshData->VertexData = vertexDataList;
        meshData->VertexIndices = vertexIndicesList;

        return meshData;
    }
}

