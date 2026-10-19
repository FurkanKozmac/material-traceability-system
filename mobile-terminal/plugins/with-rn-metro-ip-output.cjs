const { createRunOncePlugin, withXcodeProject } = require('@expo/config-plugins');

const PLUGIN_NAME = 'with-rn-metro-ip-output';
const IP_OUTPUT_PATH = '"$(CONFIGURATION_BUILD_DIR)/$(UNLOCALIZED_RESOURCES_FOLDER_PATH)/ip.txt"';

function withRnMetroIpOutput(config) {
  return withXcodeProject(config, (config) => {
    const phases = config.modResults.hash.project.objects.PBXShellScriptBuildPhase ?? {};
    const bundlePhase = Object.values(phases).find(
      (phase) => phase.name === '"Bundle React Native code and images"'
    );

    if (!bundlePhase) {
      throw new Error(`Could not find the ${PLUGIN_NAME} build phase.`);
    }

    const outputPaths = bundlePhase.outputPaths ?? [];
    if (!outputPaths.includes(IP_OUTPUT_PATH)) {
      outputPaths.push(IP_OUTPUT_PATH);
    }
    bundlePhase.outputPaths = outputPaths;

    return config;
  });
}

module.exports = createRunOncePlugin(withRnMetroIpOutput, PLUGIN_NAME, '1.0.0');
