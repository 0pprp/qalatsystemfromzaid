<script setup>
import axios from 'axios'
import { ref } from 'vue'

// Get the API base URL from localStorage
const apiUrl = localStorage.getItem('LinkCity')
const database = localStorage.getItem('Database')

// Reactive variables
const backupLink = ref('')
const now = new Date()
const year = now.getFullYear()
const month = String(now.getMonth() + 1).padStart(2, '0')
const day = String(now.getDate()).padStart(2, '0')
const hours = String(now.getHours() % 12 || 12).padStart(2, '0')
const minutes = String(now.getMinutes()).padStart(2, '0')
const seconds = String(now.getSeconds()).padStart(2, '0')
const ampm = now.getHours() >= 12 ? 'PM' : 'AM'
const backupFileName = ref(`${database} ${year}-${month}-${day} ${hours}:${minutes}:${seconds} ${ampm}.bak`)
const backupError = ref('')
const loading = ref(false)

async function backupDatabase() {
  backupError.value = ''
  loading.value = true
  try {
    const response = await axios.get(`${apiUrl}Backup/BackupDatabase`, {
      responseType: 'blob',
    })

    backupLink.value = window.URL.createObjectURL(new Blob([response.data]))
  } catch (error) {
    backupError.value = error.response?.data || error.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <VCard
    class="pa-5"
    style=" margin: auto;max-inline-size: 600px;"
  >
    <VRow class="justify-center">
      <VCol
        cols="12"
        class="text-center"
      >
        <img
          src="https://images.icon-icons.com/37/PNG/512/databackup_theapplication_dedatos_3366.png"
          alt="Backup"
          style=" block-size: auto;max-inline-size: 50%;"
        >
        <h2 style="margin-block-start: 20px;">
          النسخ الاحتياطي
        </h2>
      </VCol>
    </VRow>

    <VRow
      class="justify-center"
      style="margin-block-start: 20px;"
    >
      <VCol
        cols="12"
        class="text-center"
      >
        <VBtn
          color="primary"
          :loading="loading"
          @click="backupDatabase"
        >
          <VIcon left>
            mdi-database
          </VIcon>
          اضغط للنسخ الاحتياطي
        </VBtn>
      </VCol>
    </VRow>

    <!-- Show the download button if backupLink is available -->
    <VRow
      v-if="backupLink"
      class="justify-center"
      style="margin-block-start: 20px;"
    >
      <VCol
        cols="12"
        class="text-center"
      >
        <a
          :href="backupLink"
          :download="backupFileName"
          target="_blank"
        >
          <VBtn color="success">
            <VIcon left>mdi-download</VIcon>
            تحميل ملف النسخ الاحتياطي
          </VBtn>
        </a>
      </VCol>
    </VRow>

    <!-- Display error message if any -->
    <VRow
      v-if="backupError"
      class="justify-center"
      style="margin-block-start: 20px;"
    >
      <VCol
        cols="12"
        class="text-center"
      >
        <VAlert type="error">
          {{ backupError }}
        </VAlert>
      </VCol>
    </VRow>
  </VCard>
</template>

<style scoped>
/* Add custom styling as needed */
</style>
