const { getClients } = require('./_google');
const { getLabConfig } = require('./_registry');

exports.handler = async (event) => {
  try {
    const lab = (event.queryStringParameters?.lab || '').trim();
    const patientId = (event.queryStringParameters?.id || '').trim();

    if (!lab) return json(400, { error: 'missing_lab', message: 'Missing lab' });
    if (!patientId) return json(400, { error: 'missing_id', message: 'Missing patient id' });

    const cfg = await getLabConfig(lab);
    if (!cfg) return json(404, { error: 'lab_not_found', message: 'LabKey not found' });
    if (!cfg.driveFolderId) return json(500, { error: 'lab_not_ready', message: 'DriveFolderId not configured for this lab' });

    const { drive } = getClients();

    // 1) Find patient folder under lab root
    const qFolder = [
      `'${cfg.driveFolderId}' in parents`,
      `name = '${escapeQ(patientId)}'`,
      `mimeType = 'application/vnd.google-apps.folder'`,
      `trashed = false`
    ].join(' and ');

    const folderRes = await drive.files.list({
      q: qFolder,
      fields: 'files(id,name)',
      pageSize: 5,
      includeItemsFromAllDrives: true,
      supportsAllDrives: true,
    });

    const patientFolder = folderRes.data.files?.[0];
    if (!patientFolder) {
      return json(404, { error: 'patient_folder_not_found', message: 'Patient folder not found' });
    }

    // 2) List files inside patient folder
    const qFiles = [
      `'${patientFolder.id}' in parents`,
      `trashed = false`,
      `mimeType != 'application/vnd.google-apps.folder'`
    ].join(' and ');

    const filesRes = await drive.files.list({
      q: qFiles,
      fields: 'files(id,name,mimeType,size,modifiedTime)',
      orderBy: 'modifiedTime desc',
      pageSize: 200,
      includeItemsFromAllDrives: true,
      supportsAllDrives: true,
    });

    const files = (filesRes.data.files || []).map(f => ({
      id: f.id,
      name: f.name,
      mimeType: f.mimeType,
      size: f.size,
      modifiedTime: f.modifiedTime,
    }));

    return json(200, {
      labKey: cfg.labKey,
      patientId,
      folderId: patientFolder.id,
      files,
    });
  } catch (e) {
    return json(500, { error: 'server_error', message: e.message || String(e) });
  }
};

function escapeQ(s) {
  // Escape single quotes for Drive query
  return String(s).replace(/'/g, "\\'");
}

function json(statusCode, body) {
  return {
    statusCode,
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'cache-control': 'no-store',
    },
    body: JSON.stringify(body),
  };
}
