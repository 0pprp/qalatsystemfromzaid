<template>
  <v-row dense class="product-grid">
    <template v-if="loading && !items.length">
      <v-col v-for="n in skeletons" :key="'s' + n" v-bind="cols">
        <v-card class="soft-card h-100"><v-skeleton-loader type="image, list-item-two-line, button" /></v-card>
      </v-col>
    </template>
    <v-col v-for="p in items" :key="p.id" v-bind="cols">
      <ProductCard :product="p" />
    </v-col>
  </v-row>
</template>

<script setup>
import ProductCard from './ProductCard.vue'
defineProps({
  items: { type: Array, default: () => [] },
  loading: Boolean,
  skeletons: { type: Number, default: 8 },
  cols: { type: Object, default: () => ({ cols: 6, sm: 4, md: 3, lg: 3 }) },
})
</script>
